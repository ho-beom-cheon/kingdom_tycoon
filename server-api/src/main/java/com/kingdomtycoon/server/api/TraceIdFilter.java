package com.kingdomtycoon.server.api;

import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.util.UUID;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

@Component
public class TraceIdFilter extends OncePerRequestFilter {

    public static final String ATTRIBUTE = TraceIdFilter.class.getName() + ".traceId";
    public static final String HEADER = "X-Trace-Id";

    @Override
    protected void doFilterInternal(
        HttpServletRequest request,
        HttpServletResponse response,
        FilterChain filterChain
    ) throws ServletException, IOException {
        String traceId = normalize(request.getHeader(HEADER));
        request.setAttribute(ATTRIBUTE, traceId);
        response.setHeader(HEADER, traceId);
        filterChain.doFilter(request, response);
    }

    private static String normalize(String candidate) {
        if (candidate != null) {
            try {
                return UUID.fromString(candidate).toString();
            } catch (IllegalArgumentException ignored) {
                // Invalid client trace ids are replaced instead of trusted.
            }
        }
        return UUID.randomUUID().toString();
    }
}
