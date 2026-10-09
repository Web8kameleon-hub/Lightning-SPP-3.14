package com.clisonix.longo;

import java.io.Serializable;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;

public class Document implements Serializable {
    private static final long serialVersionUID = 1L;

    private String id;
    private final Map<String, Object> fields;

    public Document() {
        this.id = UUID.randomUUID().toString();
        this.fields = new LinkedHashMap<>();
    }

    public static Document of(Map<String, Object> values) {
        Document doc = new Document();
        for (Map.Entry<String, Object> entry : values.entrySet()) {
            if ("id".equals(entry.getKey()) || "_id".equals(entry.getKey())) {
                doc.setId(String.valueOf(entry.getValue()));
            } else {
                doc.put(entry.getKey(), entry.getValue());
            }
        }
        return doc;
    }

    public String getId() {
        return id;
    }

    public void setId(String id) {
        if (id != null && !id.isBlank()) {
            this.id = id;
        }
    }

    public Object get(String key) {
        if ("id".equals(key) || "_id".equals(key)) {
            return id;
        }
        return fields.get(key);
    }

    public void put(String key, Object value) {
        if ("id".equals(key) || "_id".equals(key)) {
            setId(String.valueOf(value));
            return;
        }
        fields.put(key, value);
    }

    public Map<String, Object> getFields() {
        return fields;
    }

    public Map<String, Object> toMap() {
        Map<String, Object> values = new LinkedHashMap<>();
        values.put("id", id);
        values.putAll(fields);
        return values;
    }

    public Document copy() {
        Document copy = new Document();
        copy.setId(this.id);
        for (Map.Entry<String, Object> entry : fields.entrySet()) {
            copy.put(entry.getKey(), entry.getValue());
        }
        return copy;
    }

    @Override
    public String toString() {
        return "Document{id='" + id + "', fields=" + fields + "}";
    }
}

