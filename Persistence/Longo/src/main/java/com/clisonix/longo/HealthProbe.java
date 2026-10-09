package com.clisonix.longo;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;

public final class HealthProbe {
    private HealthProbe() {
    }

    public static void main(String[] args) throws Exception {
        String port = System.getenv().getOrDefault("LONGO_PORT", "8030");
        HttpRequest request = HttpRequest.newBuilder(URI.create("http://127.0.0.1:" + port + "/health"))
                .timeout(Duration.ofSeconds(3))
                .GET()
                .build();
        HttpResponse<Void> response = HttpClient.newHttpClient().send(
                request,
                HttpResponse.BodyHandlers.discarding());
        if (response.statusCode() != 200) {
            throw new IllegalStateException("Longo health returned HTTP " + response.statusCode());
        }
    }
}

