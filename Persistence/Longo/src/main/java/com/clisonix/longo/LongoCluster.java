package com.clisonix.longo;

import java.io.Serializable;
import java.util.LinkedHashMap;
import java.util.Map;

public class LongoCluster implements Serializable {
    private static final long serialVersionUID = 1L;

    private final Map<String, LongoDatabase> databases;

    public LongoCluster() {
        this.databases = new LinkedHashMap<>();
    }

    public LongoDatabase getOrCreateDatabase(String dbName) {
        return databases.computeIfAbsent(dbName, LongoDatabase::new);
    }

    public Map<String, LongoDatabase> getDatabases() {
        return databases;
    }

    LongoCluster copy() {
        LongoCluster copy = new LongoCluster();
        for (Map.Entry<String, LongoDatabase> entry : databases.entrySet()) {
            copy.databases.put(entry.getKey(), entry.getValue().copy());
        }
        return copy;
    }
}

