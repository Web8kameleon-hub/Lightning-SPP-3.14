package com.clisonix.longo;

import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.Callable;
import java.util.concurrent.Executors;

public final class LongoRuntimeTest {
    private LongoRuntimeTest() {
    }

    public static void main(String[] args) throws Exception {
        testJsonRoundTrip();
        testDurableStoreAndDuplicateProtection();
        testCorruptionFailsOpenly();
        testFailedSaveRollsBackMemory();
        testConcurrentWrites();
        testHttpCrudAndRestartPersistence();
        System.out.println("Longo runtime tests passed");
    }

    private static void testJsonRoundTrip() {
        String source = "{\"name\":\"Ana\",\"active\":true,\"age\":31,\"tags\":[\"eeg\",\"audio\"],\"meta\":null}";
        Object decoded = JsonCodec.parse(source);
        Object roundTrip = JsonCodec.parse(JsonCodec.stringify(decoded));
        check(decoded.equals(roundTrip), "JSON round trip changed the document");

        Object integerValue = JsonCodec.parse("1");
        Object decimalValue = JsonCodec.parse("1.0");
        check(integerValue instanceof Long, "Integer JSON values must remain Long, not Double");
        check(Long.valueOf(1L).equals(integerValue), "Integer JSON value 1 was not preserved as 1");
        check(decimalValue instanceof Double, "Decimal JSON values must remain Double");
        check(Double.valueOf(1.0d).equals(decimalValue), "Decimal JSON value 1.0 was not preserved as 1.0");
    }

    private static void testDurableStoreAndDuplicateProtection() throws Exception {
        Path directory = Files.createTempDirectory("longo-durable-");
        Path storage = directory.resolve("longo.bin");
        LongoStore store = new LongoStore(storage);
        Map<String, Object> values = document("doc-1", "signal");

        store.insert("clisonix", "artifacts", values);
        LongoStore reloaded = new LongoStore(storage);
        Document found = reloaded.get("clisonix", "artifacts", "doc-1");
        check(found != null, "Persisted document was not reloaded");
        check("signal".equals(found.get("kind")), "Persisted document fields changed");

        boolean rejected = false;
        try {
            reloaded.insert("clisonix", "artifacts", values);
        } catch (IllegalArgumentException expected) {
            rejected = true;
        }
        check(rejected, "Duplicate document id was not rejected");
    }

    private static void testCorruptionFailsOpenly() throws Exception {
        Path storage = Files.createTempFile("longo-corrupt-", ".bin");
        Files.writeString(storage, "not-a-longo-store", StandardCharsets.UTF_8);
        boolean failed = false;
        try {
            new LongoStore(storage);
        } catch (IOException expected) {
            failed = true;
        }
        check(failed, "Corrupt storage was silently replaced with an empty database");
    }

    private static void testFailedSaveRollsBackMemory() throws Exception {
        Path storage = Files.createTempDirectory("longo-failed-save-").resolve("longo.bin");
        LongoStore store = new LongoStore(new FailingStorageEngine(storage));
        boolean failed = false;
        try {
            store.insert("clisonix", "events", document("not-persisted", "audit"));
        } catch (IOException expected) {
            failed = true;
        }
        check(failed, "Storage failure was not reported");
        check(
                store.get("clisonix", "events", "not-persisted") == null,
                "Failed storage write remained visible in memory");
    }

    private static void testConcurrentWrites() throws Exception {
        Path storage = Files.createTempDirectory("longo-concurrent-").resolve("longo.bin");
        LongoStore store = new LongoStore(storage);
        List<Callable<Void>> writes = new ArrayList<>();
        for (int index = 0; index < 40; index++) {
            int documentIndex = index;
            writes.add(() -> {
                store.insert("clisonix", "events", document("event-" + documentIndex, "audit"));
                return null;
            });
        }
        try (var executor = Executors.newFixedThreadPool(8)) {
            executor.invokeAll(writes).forEach(future -> {
                try {
                    future.get();
                } catch (Exception e) {
                    throw new AssertionError("Concurrent write failed", e);
                }
            });
        }
        LongoStore reloaded = new LongoStore(storage);
        check(reloaded.findAll("clisonix", "events").size() == 40, "Concurrent writes were lost");
    }

    private static void testHttpCrudAndRestartPersistence() throws Exception {
        Path storage = Files.createTempDirectory("longo-http-").resolve("longo.bin");
        LongoServer server = new LongoServer(storage, "127.0.0.1", 0);
        server.start();
        int port = server.getPort();
        HttpClient client = HttpClient.newHttpClient();
        String collectionUrl = "http://127.0.0.1:" + port
                + "/v1/databases/clisonix/collections/artifacts/documents";

        HttpResponse<String> health = send(client, "GET", "http://127.0.0.1:" + port + "/health", null);
        check(health.statusCode() == 200, "Health endpoint did not verify writable storage");

        HttpResponse<String> created = send(
                client,
                "POST",
                collectionUrl,
                JsonCodec.stringify(document("http-1", "report")));
        check(created.statusCode() == 201, "HTTP insert failed: " + created.body());
        HttpResponse<String> duplicate = send(
                client,
                "POST",
                collectionUrl,
                JsonCodec.stringify(document("http-1", "report")));
        check(duplicate.statusCode() == 409, "Duplicate HTTP insert did not return 409");
        HttpResponse<String> queried = send(
                client,
                "POST",
                collectionUrl + "/query",
                "{\"where\":{\"kind\":\"report\"},\"limit\":1}");
        check(queried.statusCode() == 200, "HTTP query failed: " + queried.body());
        check(queried.body().contains("\"id\":\"http-1\""), "HTTP query returned the wrong document");
        server.stop(0);

        LongoServer restarted = new LongoServer(storage, "127.0.0.1", 0);
        restarted.start();
        try {
            String documentUrl = "http://127.0.0.1:" + restarted.getPort()
                    + "/v1/databases/clisonix/collections/artifacts/documents/http-1";
            HttpResponse<String> loaded = send(client, "GET", documentUrl, null);
            check(loaded.statusCode() == 200, "HTTP document was not durable across restart");
            check(loaded.body().contains("\"kind\":\"report\""), "HTTP document content changed");

            HttpResponse<String> deleted = send(client, "DELETE", documentUrl, null);
            check(deleted.statusCode() == 204, "HTTP delete failed");
            HttpResponse<String> missing = send(client, "GET", documentUrl, null);
            check(missing.statusCode() == 404, "Deleted HTTP document remained visible");
        } finally {
            restarted.stop(0);
        }
    }

    private static HttpResponse<String> send(
            HttpClient client,
            String method,
            String url,
            String body) throws IOException, InterruptedException {
        HttpRequest.Builder request = HttpRequest.newBuilder(URI.create(url));
        if (body == null) {
            request.method(method, HttpRequest.BodyPublishers.noBody());
        } else {
            request.header("Content-Type", "application/json");
            request.method(method, HttpRequest.BodyPublishers.ofString(body));
        }
        return client.send(request.build(), HttpResponse.BodyHandlers.ofString());
    }

    private static Map<String, Object> document(String id, String kind) {
        Map<String, Object> values = new LinkedHashMap<>();
        values.put("id", id);
        values.put("kind", kind);
        values.put("active", true);
        return values;
    }

    private static void check(boolean condition, String message) {
        if (!condition) {
            throw new AssertionError(message);
        }
    }

    private static final class FailingStorageEngine extends StorageEngine {
        private FailingStorageEngine(Path storageFile) {
            super(storageFile);
        }

        @Override
        public void save(LongoCluster cluster) throws IOException {
            throw new IOException("injected storage failure");
        }
    }
}

