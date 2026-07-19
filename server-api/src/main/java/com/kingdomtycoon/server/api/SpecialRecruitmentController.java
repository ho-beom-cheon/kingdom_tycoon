package com.kingdomtycoon.server.api;

import com.kingdomtycoon.server.session.DevelopmentSessionService;
import com.kingdomtycoon.server.summon.SummonApplicationService;
import com.kingdomtycoon.server.summon.SummonReceiptQueryService;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import java.util.UUID;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/players/me/recruitments/special")
public class SpecialRecruitmentController {

    private final DevelopmentSessionService sessionService;
    private final SummonApplicationService summonService;
    private final SummonReceiptQueryService receiptQueryService;

    public SpecialRecruitmentController(
        DevelopmentSessionService sessionService,
        SummonApplicationService summonService,
        SummonReceiptQueryService receiptQueryService
    ) {
        this.sessionService = sessionService;
        this.summonService = summonService;
        this.receiptQueryService = receiptQueryService;
    }

    @PostMapping
    public ApiEnvelope.Success<SummonReceiptQueryService.Receipt> recruit(
        @RequestHeader(name = "Authorization", required = false) String authorization,
        @RequestHeader(name = "Idempotency-Key", required = false) String idempotencyKey,
        @Valid @RequestBody SpecialRecruitmentRequest body,
        HttpServletRequest request
    ) {
        if (idempotencyKey == null || idempotencyKey.isBlank()) {
            throw new MissingIdempotencyKeyException();
        }
        UUID key;
        try {
            key = UUID.fromString(idempotencyKey);
        } catch (IllegalArgumentException error) {
            throw new MissingIdempotencyKeyException();
        }
        var player = sessionService.authenticate(authorization);
        var result = summonService.summon(
            player.internalPlayerId(),
            body.bannerKey(),
            (short) body.pullCount(),
            key
        );
        return ApiResponses.success(
            request,
            receiptQueryService.load(player.internalPlayerId(), result.summonTransactionId(), result.replayed())
        );
    }

    public record SpecialRecruitmentRequest(
        @NotBlank @Pattern(regexp = "^[A-Z0-9_]+$") String bannerKey,
        @Min(1) @Max(10) int pullCount
    ) {
    }

    public static class MissingIdempotencyKeyException extends RuntimeException {
    }
}
