package com.clisonix.longo;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

public class LongoCollection implements Serializable {
    private static final long serialVersionUID = 1L;

    private final String name;
    private final Map<String, Document> documents;
    private final Set<String> indexedFields;
    private final Map<String, Map<Object, Set<String>>> indexes;

    public LongoCollection(String name) {
        this.name = name;
        this.documents = new LinkedHashMap<>();
        this.indexedFields = new HashSet<>();
        this.indexes = new HashMap<>();
    }

    public String getName() {
        return name;
    }

    public Document insert(Document doc) {
        Document stored = doc.copy();
        if (documents.containsKey(stored.getId())) {
            throw new DuplicateDocumentException(stored.getId());
        }
        documents.put(stored.getId(), stored);
        indexDocument(stored);
        return stored.copy();
    }

    public Document getById(String documentId) {
        Document document = documents.get(documentId);
        return document == null ? null : document.copy();
    }

    public Document replace(String documentId, Document replacement) {
        Document existing = documents.get(documentId);
        if (existing == null) {
            return null;
        }
        deIndexDocument(existing);
        Document stored = replacement.copy();
        stored.setId(documentId);
        documents.put(documentId, stored);
        indexDocument(stored);
        return stored.copy();
    }

    public List<Document> find(Filter filter) {
        List<Document> results = new ArrayList<>();
        for (Document doc : documents.values()) {
            if (filter.matches(doc)) {
                results.add(doc.copy());
            }
        }
        return results;
    }

    public int update(Filter filter, Map<String, Object> newValues) {
        int count = 0;
        for (Document doc : documents.values()) {
            if (filter.matches(doc)) {
                deIndexDocument(doc);
                for (Map.Entry<String, Object> entry : newValues.entrySet()) {
                    doc.put(entry.getKey(), entry.getValue());
                }
                indexDocument(doc);
                count++;
            }
        }
        return count;
    }

    public int delete(Filter filter) {
        List<String> toDelete = new ArrayList<>();
        for (Document doc : documents.values()) {
            if (filter.matches(doc)) {
                toDelete.add(doc.getId());
            }
        }
        for (String id : toDelete) {
            Document removed = documents.remove(id);
            if (removed != null) {
                deIndexDocument(removed);
            }
        }
        return toDelete.size();
    }

    public boolean deleteById(String documentId) {
        Document removed = documents.remove(documentId);
        if (removed == null) {
            return false;
        }
        deIndexDocument(removed);
        return true;
    }

    public void createIndex(String field) {
        if (indexedFields.contains(field)) {
            return;
        }
        indexedFields.add(field);
        indexes.put(field, new HashMap<>());
        for (Document doc : documents.values()) {
            addToIndex(field, doc.get(field), doc.getId());
        }
    }

    public int size() {
        return documents.size();
    }

    LongoCollection copy() {
        LongoCollection copy = new LongoCollection(name);
        for (String field : indexedFields) {
            copy.createIndex(field);
        }
        for (Document document : documents.values()) {
            copy.insert(document);
        }
        return copy;
    }

    private void indexDocument(Document doc) {
        for (String field : indexedFields) {
            addToIndex(field, doc.get(field), doc.getId());
        }
    }

    private void deIndexDocument(Document doc) {
        for (String field : indexedFields) {
            removeFromIndex(field, doc.get(field), doc.getId());
        }
    }

    private void addToIndex(String field, Object value, String docId) {
        Map<Object, Set<String>> idx = indexes.get(field);
        if (idx == null) {
            return;
        }
        idx.computeIfAbsent(value, k -> new HashSet<>()).add(docId);
    }

    private void removeFromIndex(String field, Object value, String docId) {
        Map<Object, Set<String>> idx = indexes.get(field);
        if (idx == null) {
            return;
        }
        Set<String> ids = idx.get(value);
        if (ids == null) {
            return;
        }
        ids.remove(docId);
        if (ids.isEmpty()) {
            idx.remove(value);
        }
    }
}

