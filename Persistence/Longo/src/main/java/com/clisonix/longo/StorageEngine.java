package com.clisonix.longo;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InvalidObjectException;
import java.io.ObjectInputFilter;
import java.io.ObjectInputStream;
import java.io.ObjectOutputStream;
import java.nio.file.AtomicMoveNotSupportedException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;

public class StorageEngine {
    private final Path storageFile;

    public StorageEngine(Path storageFile) {
        this.storageFile = storageFile;
    }

    public LongoCluster loadOrCreate() throws IOException {
        if (!Files.exists(storageFile)) {
            return new LongoCluster();
        }
        try (ObjectInputStream in = new ObjectInputStream(
                new BufferedInputStream(new FileInputStream(storageFile.toFile())))) {
            in.setObjectInputFilter(StorageEngine::filterSerializedClass);
            Object obj = in.readObject();
            if (obj instanceof LongoCluster) {
                return (LongoCluster) obj;
            }
            throw new InvalidObjectException("Longo storage does not contain a LongoCluster");
        } catch (ClassNotFoundException e) {
            throw new IOException("Longo storage contains an unavailable class", e);
        }
    }

    public void save(LongoCluster cluster) throws IOException {
        Path parent = storageFile.getParent();
        if (parent != null) {
            Files.createDirectories(parent);
        }
        Path directory = parent == null ? Path.of(".") : parent;
        Path temporaryFile = Files.createTempFile(directory, storageFile.getFileName().toString(), ".tmp");
        try {
            try (FileOutputStream fileOutput = new FileOutputStream(temporaryFile.toFile());
                    ObjectOutputStream out = new ObjectOutputStream(new BufferedOutputStream(fileOutput))) {
                out.writeObject(cluster);
                out.flush();
                fileOutput.getFD().sync();
            }
            moveAtomically(temporaryFile, storageFile);
        } finally {
            Files.deleteIfExists(temporaryFile);
        }
    }

    public Path getStorageFile() {
        return storageFile;
    }

    private static void moveAtomically(Path source, Path destination) throws IOException {
        try {
            Files.move(
                    source,
                    destination,
                    StandardCopyOption.ATOMIC_MOVE,
                    StandardCopyOption.REPLACE_EXISTING);
        } catch (AtomicMoveNotSupportedException e) {
            Files.move(source, destination, StandardCopyOption.REPLACE_EXISTING);
        }
    }

    private static ObjectInputFilter.Status filterSerializedClass(ObjectInputFilter.FilterInfo info) {
        if (info.depth() > 64 || info.references() > 1_000_000 || info.arrayLength() > 1_000_000) {
            return ObjectInputFilter.Status.REJECTED;
        }
        Class<?> serialClass = info.serialClass();
        if (serialClass == null || serialClass.isPrimitive()) {
            return ObjectInputFilter.Status.UNDECIDED;
        }
        if (serialClass.isArray()) {
            serialClass = serialClass.getComponentType();
            if (serialClass == null || serialClass.isPrimitive()) {
                return ObjectInputFilter.Status.ALLOWED;
            }
        }
        String name = serialClass.getName();
        if (name.startsWith("com.clisonix.longo.")
                || name.startsWith("java.lang.")
                || name.startsWith("java.util.")) {
            return ObjectInputFilter.Status.ALLOWED;
        }
        return ObjectInputFilter.Status.REJECTED;
    }
}

