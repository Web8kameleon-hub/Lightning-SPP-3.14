package com.clisonix.longo;

import java.io.Serializable;

@FunctionalInterface
public interface Filter extends Serializable {
    boolean matches(Document doc);
}

