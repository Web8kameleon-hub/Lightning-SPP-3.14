package com.clisonix.longo;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.Map;
import java.util.concurrent.locks.ReentrantReadWriteLock;

public final class LongoStore {
    private final StorageEngine storage;
    private LongoCluster cluster;
    private final ReentrantReadWriteLock lock = new ReentrantReadWriteLock(true);

    public LongoStore(Path storagePath) throws IOException {
        this(new StorageEngine(storagePath));
    }

    LongoStore(StorageEngine storage) throws IOException {
        this.storage = storage;
        this.cluster = storage.loadOrCreate();
    }

    public Document insert(String database, String collection, Map<String, Object> values) throws IOException {
        lock.writeLock().lock();
        try {
            LongoCluster workingCopy = cluster.copy();
            Document inserted = getOrCreateCollection(workingCopy, database, collection)
                    .insert(Document.of(values));
            storage.save(workingCopy);
            cluster = workingCopy;
            return inserted;
        } finally {
            lock.writeLock().unlock();
        }
    }

    // Commit the entire Lightning aggregate under one durable write. A stale writer never wins.
    public Document compareAndSwap(String id, long expectedRevision, Map<String, Object> values) throws IOException {
        lock.writeLock().lock();
        try {
            LongoCollection current = getCollection("lightning", "state");
            Document existing = current == null ? null : current.getById(id);
            long revision = existing == null ? 0 : ((Number) existing.get("revision")).longValue();
            if (revision != expectedRevision) return null;
            LongoCluster workingCopy = cluster.copy();
            LongoCollection target = getOrCreateCollection(workingCopy, "lightning", "state");
            Map<String, Object> next = new java.util.LinkedHashMap<>(values);
            next.put("id", id);
            next.put("revision", Math.addExact(revision, 1));
            Document committed = existing == null ? target.insert(Document.of(next)) : target.replace(id, Document.of(next));
            storage.save(workingCopy);
            cluster = workingCopy;
            return committed;
        } finally {
            lock.writeLock().unlock();
        }
    }

    public Document get(String database, String collection, String documentId) {
        lock.readLock().lock();
        try {
            LongoCollection target = getCollection(database, collection);
            return target == null ? null : target.getById(documentId);
        } finally {
            lock.readLock().unlock();
        }
    }

    public List<Document> findAll(String database, String collection) {
        lock.readLock().lock();
        try {
            LongoCollection target = getCollection(database, collection);
            return target == null ? List.of() : target.find(Filters.all());
        } finally {
            lock.readLock().unlock();
        }
    }

    public List<Document> findExact(
            String database,
            String collection,
            Map<String, Object> fields,
            Sort sort,
            int limit) {
        lock.readLock().lock();
        try {
            LongoCollection target = getCollection(database, collection);
            if (target == null) {
                return List.of();
            }
            Filter filter = document -> fields.entrySet().stream()
                    .allMatch(entry -> valuesEqual(document.get(entry.getKey()), entry.getValue()));
            List<Document> matches = target.find(filter);
            if (sort != null) {
                matches.sort(sort::compare);
            }
            return matches.size() <= limit ? matches : new java.util.ArrayList<>(matches.subList(0, limit));
        } finally {
            lock.readLock().unlock();
        }
    }

    public Document replace(
            String database,
            String collection,
            String documentId,
            Map<String, Object> values) throws IOException {
        lock.writeLock().lock();
        try {
            LongoCluster workingCopy = cluster.copy();
            LongoCollection target = getCollection(workingCopy, database, collection);
            if (target == null) {
                return null;
            }
            Document replaced = target.replace(documentId, Document.of(values));
            if (replaced != null) {
                storage.save(workingCopy);
                cluster = workingCopy;
            }
            return replaced;
        } finally {
            lock.writeLock().unlock();
        }
    }

    public boolean delete(String database, String collection, String documentId) throws IOException {
        lock.writeLock().lock();
        try {
            LongoCluster workingCopy = cluster.copy();
            LongoCollection target = getCollection(workingCopy, database, collection);
            if (target == null || !target.deleteById(documentId)) {
                return false;
            }
            storage.save(workingCopy);
            cluster = workingCopy;
            return true;
        } finally {
            lock.writeLock().unlock();
        }
    }

    public boolean isStorageReady() {
        Path storageFile = storage.getStorageFile().toAbsolutePath();
        Path parent = storageFile.getParent();
        return parent != null
                && (Files.exists(parent) ? Files.isDirectory(parent) && Files.isWritable(parent) : canCreate(parent));
    }

    public Path getStoragePath() {
        return storage.getStorageFile().toAbsolutePath();
    }

    private LongoCollection getOrCreateCollection(
            LongoCluster targetCluster,
            String database,
            String collection) {
        return targetCluster.getOrCreateDatabase(database).getOrCreateCollection(collection);
    }

    private LongoCollection getCollection(String database, String collection) {
        return getCollection(cluster, database, collection);
    }

    private LongoCollection getCollection(
            LongoCluster targetCluster,
            String database,
            String collection) {
        LongoDatabase targetDatabase = targetCluster.getDatabases().get(database);
        return targetDatabase == null ? null : targetDatabase.getCollection(collection);
    }

    private boolean canCreate(Path directory) {
        Path current = directory;
        while (current != null && !Files.exists(current)) {
            current = current.getParent();
        }
        return current != null && Files.isDirectory(current) && Files.isWritable(current);
    }

    private static boolean valuesEqual(Object left, Object right) {
        if (left instanceof Number && right instanceof Number) {
            return Double.compare(
                    ((Number) left).doubleValue(),
                    ((Number) right).doubleValue()) == 0;
        }
        return java.util.Objects.equals(left, right);
    }
}

