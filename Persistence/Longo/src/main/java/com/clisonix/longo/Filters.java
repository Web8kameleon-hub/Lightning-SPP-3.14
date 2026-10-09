package com.clisonix.longo;

import java.util.Arrays;
import java.util.HashSet;
import java.util.Set;

public final class Filters {
    private Filters() {
    }

    public static Filter all() {
        return doc -> true;
    }

    public static Filter eq(String field, Object value) {
        return doc -> safeEquals(doc.get(field), value);
    }

    public static Filter ne(String field, Object value) {
        return doc -> !safeEquals(doc.get(field), value);
    }

    public static Filter gt(String field, Object value) {
        return doc -> compare(doc.get(field), value) > 0;
    }

    public static Filter gte(String field, Object value) {
        return doc -> compare(doc.get(field), value) >= 0;
    }

    public static Filter lt(String field, Object value) {
        return doc -> compare(doc.get(field), value) < 0;
    }

    public static Filter lte(String field, Object value) {
        return doc -> compare(doc.get(field), value) <= 0;
    }

    public static Filter in(String field, Object... values) {
        Set<Object> set = new HashSet<>(Arrays.asList(values));
        return doc -> set.contains(doc.get(field));
    }

    public static Filter and(Filter... filters) {
        return doc -> {
            for (Filter f : filters) {
                if (!f.matches(doc)) {
                    return false;
                }
            }
            return true;
        };
    }

    public static Filter or(Filter... filters) {
        return doc -> {
            for (Filter f : filters) {
                if (f.matches(doc)) {
                    return true;
                }
            }
            return false;
        };
    }

    private static boolean safeEquals(Object left, Object right) {
        if (left == null && right == null) {
            return true;
        }
        if (left == null || right == null) {
            return false;
        }
        if (left instanceof Number && right instanceof Number) {
            return Double.compare(((Number) left).doubleValue(), ((Number) right).doubleValue()) == 0;
        }
        return left.equals(right);
    }

    @SuppressWarnings({"rawtypes", "unchecked"})
    private static int compare(Object left, Object right) {
        if (left == null || right == null) {
            return -1;
        }
        if (left instanceof Number && right instanceof Number) {
            return Double.compare(((Number) left).doubleValue(), ((Number) right).doubleValue());
        }
        if (left instanceof Comparable && right instanceof Comparable) {
            try {
                return ((Comparable) left).compareTo(right);
            } catch (ClassCastException ignored) {
                return String.valueOf(left).compareTo(String.valueOf(right));
            }
        }
        return String.valueOf(left).compareTo(String.valueOf(right));
    }
}

