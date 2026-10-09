package com.clisonix.longo;

import java.io.Serializable;
import java.util.LinkedHashMap;
import java.util.Map;

public class LongoDatabase implements Serializable {
    private static final long serialVersionUID = 1L;

    private final String name;
    private final Map<String, LongoCollection> collections;

    public LongoDatabase(String name) {
        this.name = name;
        this.collections = new LinkedHashMap<>();
    }

    public String getName() {
        return name;
    }

    public LongoCollection getOrCreateCollection(String collectionName) {
        return collections.computeIfAbsent(collectionName, LongoCollection::new);
    }

    public LongoCollection getCollection(String collectionName) {
        return collections.get(collectionName);
    }

    public Map<String, LongoCollection> getCollections() {
        return collections;
    }

    LongoDatabase copy() {
        LongoDatabase copy = new LongoDatabase(name);
        for (Map.Entry<String, LongoCollection> entry : collections.entrySet()) {
            copy.collections.put(entry.getKey(), entry.getValue().copy());
        }
        return copy;
    }
}

