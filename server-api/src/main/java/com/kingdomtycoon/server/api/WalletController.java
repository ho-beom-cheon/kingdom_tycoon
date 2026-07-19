package com.kingdomtycoon.server.api;

import com.kingdomtycoon.server.session.DevelopmentSessionService;
import com.kingdomtycoon.server.wallet.WalletRepository;
import jakarta.servlet.http.HttpServletRequest;
import java.util.List;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/players/me/wallet")
public class WalletController {

    private final DevelopmentSessionService sessionService;
    private final WalletRepository walletRepository;

    public WalletController(DevelopmentSessionService sessionService, WalletRepository walletRepository) {
        this.sessionService = sessionService;
        this.walletRepository = walletRepository;
    }

    @GetMapping
    public ApiEnvelope.Success<WalletResponse> get(
        @RequestHeader(name = "Authorization", required = false) String authorization,
        HttpServletRequest request
    ) {
        var player = sessionService.authenticate(authorization);
        List<BalanceResponse> balances = walletRepository.findAll(player.internalPlayerId()).stream()
            .map(value -> new BalanceResponse(value.currencyKey(), value.balance(), value.version()))
            .toList();
        return ApiResponses.success(request, new WalletResponse(player.playerId().toString(), balances));
    }

    public record WalletResponse(String playerId, List<BalanceResponse> balances) {
    }

    public record BalanceResponse(String currencyKey, long balance, long version) {
    }
}
