package com.clisonix.longo;

import java.io.IOException;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Scanner;

public class Main {
    private final StorageEngine storage;
    private final LongoCluster cluster;

    private LongoDatabase currentDb;

    public Main(Path storagePath) throws IOException {
        this.storage = new StorageEngine(storagePath);
        this.cluster = storage.loadOrCreate();
        this.currentDb = cluster.getOrCreateDatabase("default");
    }

    public static void main(String[] args) throws IOException {
        Main app = new Main(Path.of("data", "longo.bin"));
        app.run();
    }

    public void run() {
        System.out.println("LongoDB CLI - Mongo-like Java engine");
        System.out.println("Type commands: use/create/index/insert/find/update/delete/save/exit");

        Scanner scanner = new Scanner(System.in);
        while (true) {
            System.out.print("longo(" + currentDb.getName() + ")> ");
            if (!scanner.hasNextLine()) {
                break;
            }
            String line = scanner.nextLine().trim();
            if (line.isEmpty()) {
                continue;
            }
            try {
                boolean shouldExit = handleCommand(line);
                if (shouldExit) {
                    break;
                }
            } catch (Exception e) {
                System.out.println("error: " + e.getMessage());
            }
        }
    }

    private boolean handleCommand(String line) throws IOException {
        String lower = line.toLowerCase(Locale.ROOT);
        if (lower.equals("exit")) {
            save();
            System.out.println("bye");
            return true;
        }
        if (lower.equals("save")) {
            save();
            System.out.println("saved");
            return false;
        }
        if (lower.startsWith("use ")) {
            String dbName = line.substring(4).trim();
            requireNotBlank(dbName, "database name required");
            currentDb = cluster.getOrCreateDatabase(dbName);
            System.out.println("using database: " + currentDb.getName());
            return false;
        }
        if (lower.startsWith("create ")) {
            String collection = line.substring(7).trim();
            requireNotBlank(collection, "collection name required");
            currentDb.getOrCreateCollection(collection);
            System.out.println("collection ready: " + collection);
            return false;
        }
        if (lower.startsWith("index ")) {
            String payload = line.substring(6).trim();
            String[] parts = payload.split("\\s+", 2);
            if (parts.length < 2) {
                throw new IllegalArgumentException("usage: index <collection> <field>");
            }
            LongoCollection col = requireCollection(parts[0]);
            col.createIndex(parts[1].trim());
            System.out.println("index created on " + parts[0] + "." + parts[1].trim());
            return false;
        }
        if (lower.startsWith("insert ")) {
            handleInsert(line.substring(7).trim());
            return false;
        }
        if (lower.startsWith("find ")) {
            handleFind(line.substring(5).trim());
            return false;
        }
        if (lower.startsWith("update ")) {
            handleUpdate(line.substring(7).trim());
            return false;
        }
        if (lower.startsWith("delete ")) {
            handleDelete(line.substring(7).trim());
            return false;
        }

        throw new IllegalArgumentException("unknown command");
    }

    private void handleInsert(String payload) {
        String[] parts = payload.split("\\s+", 2);
        if (parts.length < 2) {
            throw new IllegalArgumentException("usage: insert <collection> <k=v;...>");
        }
        LongoCollection col = currentDb.getOrCreateCollection(parts[0]);
        Map<String, Object> values = parseKeyValuePairs(parts[1]);
        Document doc = Document.of(values);
        Document inserted = col.insert(doc);
        System.out.println("inserted: " + inserted);
    }

    private void handleFind(String payload) {
        String[] first = payload.split("\\s+", 2);
        String collectionName = first[0];
        LongoCollection col = requireCollection(collectionName);

        Filter filter = Filters.all();
        if (first.length == 2) {
            String rest = first[1].trim();
            if (rest.toLowerCase(Locale.ROOT).startsWith("where ")) {
                String expr = rest.substring(6).trim();
                filter = parseFilterExpression(expr);
            }
        }

        List<Document> docs = col.find(filter);
        System.out.println("count=" + docs.size());
        for (Document d : docs) {
            System.out.println(d);
        }
    }

    private void handleUpdate(String payload) {
        String lower = payload.toLowerCase(Locale.ROOT);
        int whereIdx = lower.indexOf(" where ");
        int setIdx = lower.indexOf(" set ");
        if (whereIdx <= 0 || setIdx <= whereIdx) {
            throw new IllegalArgumentException("usage: update <collection> where <expr> set <k=v;...>");
        }

        String collectionName = payload.substring(0, whereIdx).trim();
        String whereExpr = payload.substring(whereIdx + 7, setIdx).trim();
        String setExpr = payload.substring(setIdx + 5).trim();

        LongoCollection col = requireCollection(collectionName);
        Filter filter = parseFilterExpression(whereExpr);
        Map<String, Object> values = parseKeyValuePairs(setExpr);

        int updated = col.update(filter, values);
        System.out.println("updated=" + updated);
    }

    private void handleDelete(String payload) {
        String lower = payload.toLowerCase(Locale.ROOT);
        int whereIdx = lower.indexOf(" where ");
        if (whereIdx <= 0) {
            throw new IllegalArgumentException("usage: delete <collection> where <expr>");
        }

        String collectionName = payload.substring(0, whereIdx).trim();
        String whereExpr = payload.substring(whereIdx + 7).trim();

        LongoCollection col = requireCollection(collectionName);
        Filter filter = parseFilterExpression(whereExpr);

        int deleted = col.delete(filter);
        System.out.println("deleted=" + deleted);
    }

    private LongoCollection requireCollection(String name) {
        LongoCollection col = currentDb.getCollection(name);
        if (col == null) {
            throw new IllegalArgumentException("collection not found: " + name);
        }
        return col;
    }

    private Map<String, Object> parseKeyValuePairs(String input) {
        Map<String, Object> out = new LinkedHashMap<>();
        String[] pairs = input.split(";");
        for (String pair : pairs) {
            String p = pair.trim();
            if (p.isEmpty()) {
                continue;
            }
            int idx = p.indexOf('=');
            if (idx <= 0 || idx == p.length() - 1) {
                throw new IllegalArgumentException("invalid key/value pair: " + p);
            }
            String key = p.substring(0, idx).trim();
            String rawValue = p.substring(idx + 1).trim();
            out.put(key, parseValue(rawValue));
        }
        return out;
    }

    private Filter parseFilterExpression(String expr) {
        if (expr.isBlank()) {
            return Filters.all();
        }

        String[] orParts = splitByKeyword(expr, " or ");
        if (orParts.length > 1) {
            List<Filter> filters = new ArrayList<>();
            for (String part : orParts) {
                filters.add(parseFilterExpression(part));
            }
            return Filters.or(filters.toArray(new Filter[0]));
        }

        String[] andParts = splitByKeyword(expr, " and ");
        if (andParts.length > 1) {
            List<Filter> filters = new ArrayList<>();
            for (String part : andParts) {
                filters.add(parseFilterExpression(part));
            }
            return Filters.and(filters.toArray(new Filter[0]));
        }

        String condition = expr.trim();
        String lowerCondition = condition.toLowerCase(Locale.ROOT);
        if (lowerCondition.contains(" in(")) {
            int inIdx = lowerCondition.indexOf(" in(");
            String field = condition.substring(0, inIdx).trim();
            String valuesPart = condition.substring(inIdx + 4).trim();
            if (!valuesPart.endsWith(")")) {
                throw new IllegalArgumentException("invalid in(...) syntax");
            }
            String inside = valuesPart.substring(0, valuesPart.length() - 1);
            String[] vals = inside.split(",");
            Object[] parsed = new Object[vals.length];
            for (int i = 0; i < vals.length; i++) {
                parsed[i] = parseValue(vals[i].trim());
            }
            return Filters.in(field, parsed);
        }

        String[] operators = new String[] {">=", "<=", "!=", "=", ">", "<"};
        for (String op : operators) {
            int idx = condition.indexOf(op);
            if (idx > 0) {
                String field = condition.substring(0, idx).trim();
                String valueRaw = condition.substring(idx + op.length()).trim();
                Object value = parseValue(valueRaw);
                return buildFilter(field, op, value);
            }
        }

        throw new IllegalArgumentException("invalid where expression: " + expr);
    }

    private Filter buildFilter(String field, String op, Object value) {
        switch (op) {
            case "=":
                return Filters.eq(field, value);
            case "!=":
                return Filters.ne(field, value);
            case ">":
                return Filters.gt(field, value);
            case ">=":
                return Filters.gte(field, value);
            case "<":
                return Filters.lt(field, value);
            case "<=":
                return Filters.lte(field, value);
            default:
                throw new IllegalArgumentException("unsupported operator: " + op);
        }
    }

    private String[] splitByKeyword(String input, String keyword) {
        String lower = input.toLowerCase(Locale.ROOT);
        List<String> parts = new ArrayList<>();
        int start = 0;
        int idx;
        while ((idx = lower.indexOf(keyword, start)) >= 0) {
            parts.add(input.substring(start, idx).trim());
            start = idx + keyword.length();
        }
        parts.add(input.substring(start).trim());
        return parts.toArray(new String[0]);
    }

    private Object parseValue(String raw) {
        if (raw.startsWith("\"") && raw.endsWith("\"") && raw.length() >= 2) {
            return raw.substring(1, raw.length() - 1);
        }
        if (raw.equalsIgnoreCase("true") || raw.equalsIgnoreCase("false")) {
            return Boolean.parseBoolean(raw);
        }
        try {
            if (raw.contains(".")) {
                return Double.parseDouble(raw);
            }
            return Long.parseLong(raw);
        } catch (NumberFormatException ignored) {
            return raw;
        }
    }

    private void save() throws IOException {
        storage.save(cluster);
    }

    private void requireNotBlank(String value, String message) {
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException(message);
        }
    }
}

