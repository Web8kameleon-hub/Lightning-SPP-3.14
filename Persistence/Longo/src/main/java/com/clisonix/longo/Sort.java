package com.clisonix.longo;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public final class Sort {
    private final List<SortField> fields;

    private Sort(List<SortField> fields) {
        this.fields = List.copyOf(fields);
    }

    public static Sort by(String field) {
        return by(field, Direction.ASC);
    }

    public static Sort by(String field, Direction direction) {
        requireField(field);
        return new Sort(Collections.singletonList(new SortField(field, direction)));
    }

    public Sort thenBy(String field) {
        return thenBy(field, Direction.ASC);
    }

    public Sort thenBy(String field, Direction direction) {
        requireField(field);
        List<SortField> nextFields = new ArrayList<>(fields);
        nextFields.add(new SortField(field, direction));
        return new Sort(nextFields);
    }

    public int compare(Document leftDocument, Document rightDocument) {
        for (SortField field : fields) {
            int comparison = compareValues(
                    leftDocument.get(field.field),
                    rightDocument.get(field.field));
            if (comparison != 0) {
                return field.direction == Direction.ASC ? comparison : -comparison;
            }
        }
        return 0;
    }

    public enum Direction {
        ASC,
        DESC
    }

    private static int compareValues(Object left, Object right) {
        if (left == null && right == null) {
            return 0;
        }
        if (left == null) {
            return -1;
        }
        if (right == null) {
            return 1;
        }
        if (left instanceof Number && right instanceof Number) {
            return Double.compare(
                    ((Number) left).doubleValue(),
                    ((Number) right).doubleValue());
        }
        if (left.getClass().equals(right.getClass()) && left instanceof Comparable<?>) {
            @SuppressWarnings("unchecked")
            Comparable<Object> comparable = (Comparable<Object>) left;
            return comparable.compareTo(right);
        }
        return String.valueOf(left).compareTo(String.valueOf(right));
    }

    private static void requireField(String field) {
        if (field == null || field.isBlank()) {
            throw new IllegalArgumentException("Sort field is required");
        }
    }

    private static final class SortField {
        private final String field;
        private final Direction direction;

        private SortField(String field, Direction direction) {
            this.field = field;
            this.direction = direction;
        }
    }
}

