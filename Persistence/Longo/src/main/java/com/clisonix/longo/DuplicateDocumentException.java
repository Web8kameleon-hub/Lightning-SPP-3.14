package com.clisonix.longo;

public final class DuplicateDocumentException extends IllegalArgumentException {
    private static final long serialVersionUID = 1L;

    public DuplicateDocumentException(String documentId) {
        super("document already exists: " + documentId);
    }
}

