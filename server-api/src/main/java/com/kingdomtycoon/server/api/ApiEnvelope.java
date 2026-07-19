package com.kingdomtycoon.server.api;

import java.time.Instant;
import java.util.List;

public final class ApiEnvelope {

    private ApiEnvelope() {
    }

    public record Success<T>(T data, String traceId, Instant serverTimeUtc) {
    }

    public record Error(
        String code,
        String messageKo,
        String traceId,
        Instant serverTimeUtc,
        List<FieldError> fieldErrors
    ) {
    }

    public record FieldError(String field, String reason) {
    }
}
