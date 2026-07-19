package com.kingdomtycoon.server.api;

import com.kingdomtycoon.server.api.SpecialRecruitmentController.MissingIdempotencyKeyException;
import com.kingdomtycoon.server.idempotency.IdempotencyConflictException;
import com.kingdomtycoon.server.session.AuthenticationException;
import com.kingdomtycoon.server.session.DevelopmentSessionDisabledException;
import com.kingdomtycoon.server.wallet.InsufficientBalanceException;
import jakarta.servlet.http.HttpServletRequest;
import java.time.Instant;
import java.util.List;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

@RestControllerAdvice
public class ApiExceptionHandler {

    @ExceptionHandler(MethodArgumentNotValidException.class)
    ResponseEntity<ApiEnvelope.Error> validation(MethodArgumentNotValidException error, HttpServletRequest request) {
        List<ApiEnvelope.FieldError> fields = error.getBindingResult().getFieldErrors().stream()
            .map(value -> new ApiEnvelope.FieldError(value.getField(), value.getDefaultMessage()))
            .toList();
        return response(HttpStatus.BAD_REQUEST, "VALIDATION_FAILED", "입력값을 확인해 주세요.", fields, request);
    }

    @ExceptionHandler(AuthenticationException.class)
    ResponseEntity<ApiEnvelope.Error> authentication(AuthenticationException error, HttpServletRequest request) {
        return response(HttpStatus.UNAUTHORIZED, "AUTH_INVALID", "개발 세션이 만료되었거나 유효하지 않습니다.", List.of(), request);
    }

    @ExceptionHandler(DevelopmentSessionDisabledException.class)
    ResponseEntity<ApiEnvelope.Error> disabled(DevelopmentSessionDisabledException error, HttpServletRequest request) {
        return response(HttpStatus.NOT_FOUND, "DEV_SESSION_DISABLED", "개발 세션 기능이 비활성화되어 있습니다.", List.of(), request);
    }

    @ExceptionHandler(MissingIdempotencyKeyException.class)
    ResponseEntity<ApiEnvelope.Error> missingKey(MissingIdempotencyKeyException error, HttpServletRequest request) {
        return response(HttpStatus.BAD_REQUEST, "IDEMPOTENCY_KEY_REQUIRED", "요청 식별 키가 필요합니다.", List.of(), request);
    }

    @ExceptionHandler(IdempotencyConflictException.class)
    ResponseEntity<ApiEnvelope.Error> idempotency(IdempotencyConflictException error, HttpServletRequest request) {
        return response(HttpStatus.CONFLICT, "IDEMPOTENCY_CONFLICT", "같은 요청 식별 키가 다른 요청에 사용되었습니다.", List.of(), request);
    }

    @ExceptionHandler(InsufficientBalanceException.class)
    ResponseEntity<ApiEnvelope.Error> balance(InsufficientBalanceException error, HttpServletRequest request) {
        return response(HttpStatus.CONFLICT, "INSUFFICIENT_BALANCE", "모집에 필요한 재화가 부족합니다.", List.of(), request);
    }

    @ExceptionHandler(IllegalArgumentException.class)
    ResponseEntity<ApiEnvelope.Error> badRequest(IllegalArgumentException error, HttpServletRequest request) {
        return response(HttpStatus.BAD_REQUEST, "DOMAIN_CONFLICT", "요청을 처리할 수 없습니다.", List.of(), request);
    }

    @ExceptionHandler(IllegalStateException.class)
    ResponseEntity<ApiEnvelope.Error> conflict(IllegalStateException error, HttpServletRequest request) {
        String code = error.getMessage() != null && error.getMessage().contains("progress")
            ? "REQUEST_IN_PROGRESS"
            : "DOMAIN_CONFLICT";
        return response(HttpStatus.CONFLICT, code, "현재 상태에서는 요청을 처리할 수 없습니다.", List.of(), request);
    }

    private static ResponseEntity<ApiEnvelope.Error> response(
        HttpStatus status,
        String code,
        String message,
        List<ApiEnvelope.FieldError> fields,
        HttpServletRequest request
    ) {
        return ResponseEntity.status(status).body(
            new ApiEnvelope.Error(code, message, ApiResponses.traceId(request), Instant.now(), fields)
        );
    }
}
