package com.kingdomtycoon.server.api;

import com.kingdomtycoon.server.session.DevelopmentSessionService;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/dev/sessions")
public class DevelopmentSessionController {

    private final DevelopmentSessionService sessionService;

    public DevelopmentSessionController(DevelopmentSessionService sessionService) {
        this.sessionService = sessionService;
    }

    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    public ApiEnvelope.Success<SessionResponse> create(
        @Valid @RequestBody CreateSessionRequest body,
        HttpServletRequest request
    ) {
        var session = sessionService.create(body.deviceInstallId(), body.locale(), body.nickname());
        return ApiResponses.success(
            request,
            new SessionResponse(
                session.playerId().toString(),
                session.accessToken(),
                session.expiresAtUtc(),
                session.contentVersion()
            )
        );
    }

    public record CreateSessionRequest(
        @NotBlank @Size(max = 128) String deviceInstallId,
        @NotBlank @Pattern(regexp = "ko-KR") String locale,
        @NotBlank @Size(max = 40) String nickname
    ) {
    }

    public record SessionResponse(
        String playerId,
        String accessToken,
        java.time.Instant expiresAtUtc,
        String contentVersion
    ) {
    }
}
