package com.clisonix.longo;

import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.Executors;

public final class LongoServer {
    private static final int MAX_REQUEST_BYTES = 8 * 1024 * 1024;
    private static final String IDENTIFIER_PATTERN = "[A-Za-z0-9._-]{1,128}";

    private final LongoStore store;
    private final HttpServer server;
    private final java.nio.channels.FileChannel writerChannel;
    private final java.nio.channels.FileLock writerLock;

    public LongoServer(Path storagePath, String host, int port) throws IOException {
        java.nio.file.Files.createDirectories(storagePath.toAbsolutePath().getParent());
        writerChannel = java.nio.channels.FileChannel.open(Path.of(storagePath + ".writer.lock"),
                java.nio.file.StandardOpenOption.CREATE, java.nio.file.StandardOpenOption.WRITE);
        writerLock = writerChannel.tryLock();
        if (writerLock == null) { writerChannel.close(); throw new IOException("Longo storage already has a writer"); }
        this.store = new LongoStore(storagePath);
        this.server = HttpServer.create(new InetSocketAddress(host, port), 0);
        this.server.createContext("/", this::handle);
        this.server.setExecutor(Executors.newVirtualThreadPerTaskExecutor());
    }

    public void start() {
        server.start();
    }

    public void stop(int delaySeconds) {
        server.stop(delaySeconds);
        try { writerLock.release(); writerChannel.close(); } catch (IOException e) { throw new java.io.UncheckedIOException(e); }
    }

    public int getPort() {
        return server.getAddress().getPort();
    }

    public static void main(String[] args) throws IOException {
        String host = environment("LONGO_HOST", "0.0.0.0");
        int port = Integer.parseInt(environment("LONGO_PORT", "8030"));
        Path storagePath = Path.of(environment("LONGO_STORAGE_PATH", "/data/longo.bin"));
        LongoServer longoServer = new LongoServer(storagePath, host, port);
        Runtime.getRuntime().addShutdownHook(new Thread(() -> longoServer.stop(1)));
        longoServer.start();
        System.out.printf("Longo listening on %s:%d using %s%n", host, port, storagePath.toAbsolutePath());
    }

    private void handle(HttpExchange exchange) throws IOException {
        try {
            addCommonHeaders(exchange);
            if ("GET".equals(exchange.getRequestMethod()) && "/health".equals(exchange.getRequestURI().getPath())) {
                handleHealth(exchange);
                return;
            }
            if (exchange.getRequestURI().getPath().startsWith("/v1/lightning/state/")) {
                String id = decodeIdentifier(exchange.getRequestURI().getPath().substring("/v1/lightning/state/".length()), "state");
                if ("GET".equals(exchange.getRequestMethod())) {
                    Document document = store.get("lightning", "state", id);
                    sendJson(exchange, 200, document == null ? Map.of("revision", 0, "data", Map.of()) : document.toMap());
                } else if ("PUT".equals(exchange.getRequestMethod())) {
                    String expected = exchange.getRequestHeaders().getFirst("If-Match");
                    if (expected == null) { sendError(exchange, 428, "PRECONDITION_REQUIRED", "If-Match revision required"); return; }
                    Map<String, Object> values = readObject(exchange);
                    Document document = store.compareAndSwap(id, Long.parseLong(expected), values);
                    if (document == null) sendError(exchange, 409, "REVISION_CONFLICT", "Retry using current state");
                    else sendJson(exchange, 200, document.toMap());
                } else sendError(exchange, 405, "METHOD_NOT_ALLOWED", "GET or PUT required");
                return;
            }
            handleDocumentRoute(exchange);
        } catch (PayloadTooLargeException e) {
            sendError(exchange, 413, "PAYLOAD_TOO_LARGE", e.getMessage());
        } catch (DuplicateDocumentException e) {
            sendError(exchange, 409, "DOCUMENT_EXISTS", e.getMessage());
        } catch (IllegalArgumentException e) {
            sendError(exchange, 400, "INVALID_REQUEST", e.getMessage());
        } catch (IOException e) {
            sendError(exchange, 503, "STORAGE_UNAVAILABLE", e.getMessage());
        } catch (RuntimeException e) {
            sendError(exchange, 500, "INTERNAL_ERROR", "Longo request failed");
        } finally {
            exchange.close();
        }
    }

    private void handleHealth(HttpExchange exchange) throws IOException {
        if (!store.isStorageReady()) {
            sendError(exchange, 503, "STORAGE_UNAVAILABLE", "Longo storage directory is not writable");
            return;
        }
        Map<String, Object> health = new LinkedHashMap<>();
        health.put("status", "healthy");
        health.put("storage_path", store.getStoragePath().toString());
        sendJson(exchange, 200, health);
    }

    private void handleDocumentRoute(HttpExchange exchange) throws IOException {
        String[] parts = exchange.getRequestURI().getPath().split("/");
        if (parts.length < 7
                || !"v1".equals(parts[1])
                || !"databases".equals(parts[2])
                || !"collections".equals(parts[4])
                || !"documents".equals(parts[6])) {
            sendError(exchange, 404, "NOT_FOUND", "Route not found");
            return;
        }

        String database = decodeIdentifier(parts[3], "database");
        String collection = decodeIdentifier(parts[5], "collection");
        String documentId = parts.length == 8 ? decodeIdentifier(parts[7], "document id") : null;
        if (parts.length > 8) {
            sendError(exchange, 404, "NOT_FOUND", "Route not found");
            return;
        }

        switch (exchange.getRequestMethod()) {
            case "POST":
                if ("query".equals(documentId)) {
                    handleQuery(exchange, database, collection);
                    break;
                }
                requireCollectionRoute(documentId);
                Document inserted = store.insert(database, collection, readObject(exchange));
                sendJson(exchange, 201, envelope(inserted));
                break;
            case "GET":
                if (documentId == null) {
                    List<Map<String, Object>> documents = new ArrayList<>();
                    for (Document document : store.findAll(database, collection)) {
                        documents.add(document.toMap());
                    }
                    sendJson(exchange, 200, envelope(documents));
                } else {
                    Document document = store.get(database, collection, documentId);
                    if (document == null) {
                        sendError(exchange, 404, "DOCUMENT_NOT_FOUND", "Document not found");
                    } else {
                        sendJson(exchange, 200, envelope(document));
                    }
                }
                break;
            case "PUT":
                requireDocumentRoute(documentId);
                Document replaced = store.replace(database, collection, documentId, readObject(exchange));
                if (replaced == null) {
                    sendError(exchange, 404, "DOCUMENT_NOT_FOUND", "Document not found");
                } else {
                    sendJson(exchange, 200, envelope(replaced));
                }
                break;
            case "DELETE":
                requireDocumentRoute(documentId);
                if (!store.delete(database, collection, documentId)) {
                    sendError(exchange, 404, "DOCUMENT_NOT_FOUND", "Document not found");
                } else {
                    exchange.sendResponseHeaders(204, -1);
                }
                break;
            default:
                exchange.getResponseHeaders().set("Allow", "GET, POST, PUT, DELETE");
                sendError(exchange, 405, "METHOD_NOT_ALLOWED", "Method not allowed");
        }
    }

    @SuppressWarnings("unchecked")
    private void handleQuery(HttpExchange exchange, String database, String collection) throws IOException {
        Map<String, Object> request = readObject(exchange);
        Object rawWhere = request.get("where");
        if (!(rawWhere instanceof Map<?, ?>)) {
            throw new IllegalArgumentException("Query requires a JSON object named 'where'");
        }
        int limit = 100;
        Object rawLimit = request.get("limit");
        if (rawLimit != null) {
            if (!(rawLimit instanceof Number)) {
                throw new IllegalArgumentException("Query limit must be a number");
            }
            limit = ((Number) rawLimit).intValue();
            if (limit < 1 || limit > 10_000) {
                throw new IllegalArgumentException("Query limit must be between 1 and 10000");
            }
        }
        Sort sort = parseSort(request.get("sort"));
        List<Map<String, Object>> documents = new ArrayList<>();
        for (Document document : store.findExact(
                database,
                collection,
                (Map<String, Object>) rawWhere,
                sort,
                limit)) {
            documents.add(document.toMap());
        }
        sendJson(exchange, 200, envelope(documents));
    }

    private Sort parseSort(Object rawSort) {
        if (rawSort == null) {
            return null;
        }
        if (!(rawSort instanceof List<?>)) {
            throw new IllegalArgumentException("Query sort must be an array");
        }
        Sort sort = null;
        for (Object rawField : (List<?>) rawSort) {
            if (!(rawField instanceof Map<?, ?>)) {
                throw new IllegalArgumentException("Each sort item must be an object");
            }
            Object fieldValue = ((Map<?, ?>) rawField).get("field");
            Object directionValue = ((Map<?, ?>) rawField).get("direction");
            if (!(fieldValue instanceof String)) {
                throw new IllegalArgumentException("Sort field must be a string");
            }
            Sort.Direction direction;
            try {
                direction = Sort.Direction.valueOf(
                        String.valueOf(directionValue == null ? "ASC" : directionValue).toUpperCase());
            } catch (IllegalArgumentException e) {
                throw new IllegalArgumentException("Sort direction must be asc or desc");
            }
            sort = sort == null
                    ? Sort.by((String) fieldValue, direction)
                    : sort.thenBy((String) fieldValue, direction);
        }
        return sort;
    }

    @SuppressWarnings("unchecked")
    private Map<String, Object> readObject(HttpExchange exchange) throws IOException {
        String contentType = exchange.getRequestHeaders().getFirst("Content-Type");
        if (contentType == null || !contentType.toLowerCase().startsWith("application/json")) {
            throw new IllegalArgumentException("Content-Type must be application/json");
        }
        byte[] body = readLimited(exchange.getRequestBody());
        Object parsed = JsonCodec.parse(new String(body, StandardCharsets.UTF_8));
        if (!(parsed instanceof Map<?, ?>)) {
            throw new IllegalArgumentException("Request body must be a JSON object");
        }
        return (Map<String, Object>) parsed;
    }

    private byte[] readLimited(InputStream input) throws IOException {
        byte[] buffer = new byte[8192];
        int total = 0;
        try (input) {
            while (true) {
                int read = input.read(buffer, total, buffer.length - total);
                if (read < 0) {
                    byte[] result = new byte[total];
                    System.arraycopy(buffer, 0, result, 0, total);
                    return result;
                }
                total += read;
                if (total == buffer.length) {
                    if (buffer.length >= MAX_REQUEST_BYTES) {
                        throw new PayloadTooLargeException("Request exceeds 8 MiB");
                    }
                    byte[] expanded = new byte[Math.min(buffer.length * 2, MAX_REQUEST_BYTES)];
                    System.arraycopy(buffer, 0, expanded, 0, total);
                    buffer = expanded;
                }
            }
        }
    }

    private static String decodeIdentifier(String encoded, String label) {
        String value = URLDecoder.decode(encoded, StandardCharsets.UTF_8);
        if (!value.matches(IDENTIFIER_PATTERN)) {
            throw new IllegalArgumentException(
                    label + " must contain only letters, numbers, dot, underscore, or hyphen");
        }
        return value;
    }

    private static void requireCollectionRoute(String documentId) {
        if (documentId != null) {
            throw new IllegalArgumentException("POST is only valid on a collection route");
        }
    }

    private static void requireDocumentRoute(String documentId) {
        if (documentId == null) {
            throw new IllegalArgumentException("Document id is required");
        }
    }

    private static Map<String, Object> envelope(Object data) {
        Map<String, Object> result = new LinkedHashMap<>();
        result.put("data", data);
        return result;
    }

    private static void addCommonHeaders(HttpExchange exchange) {
        exchange.getResponseHeaders().set("Content-Type", "application/json; charset=utf-8");
        exchange.getResponseHeaders().set("Cache-Control", "no-store");
        exchange.getResponseHeaders().set("X-Content-Type-Options", "nosniff");
    }

    private static void sendError(HttpExchange exchange, int status, String code, String message)
            throws IOException {
        Map<String, Object> error = new LinkedHashMap<>();
        error.put("code", code);
        error.put("message", message);
        Map<String, Object> response = new LinkedHashMap<>();
        response.put("error", error);
        sendJson(exchange, status, response);
    }

    private static void sendJson(HttpExchange exchange, int status, Object value) throws IOException {
        byte[] body = JsonCodec.stringify(value).getBytes(StandardCharsets.UTF_8);
        exchange.sendResponseHeaders(status, body.length);
        try (OutputStream output = exchange.getResponseBody()) {
            output.write(body);
        }
    }

    private static String environment(String name, String defaultValue) {
        String value = System.getenv(name);
        return value == null || value.isBlank() ? defaultValue : value;
    }

    private static final class PayloadTooLargeException extends IllegalArgumentException {
        private static final long serialVersionUID = 1L;

        private PayloadTooLargeException(String message) {
            super(message);
        }
    }
}

