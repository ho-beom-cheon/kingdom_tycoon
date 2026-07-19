package com.kingdomtycoon.server.api;

import jakarta.servlet.http.HttpServletRequest;
import java.time.Instant;

public final class ApiResponses {

    private ApiResponses() {
    }

    public static <T> ApiEnvelope.Success<T> success(HttpServletRequest request, T data) {
        return new ApiEnvelope.Success<>(data, traceId(request), Instant.now());
    }

    public static String traceId(HttpServletRequest request) {
        Object value = request.getAttribute(TraceIdFilter.ATTRIBUTE);
        return value == null ? "unknown" : value.toString();
    }
}
