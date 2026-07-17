package com.kingdomtycoon.server.summon;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface SummonRepository {

    Banner findActiveBanner(String bannerKey, String channelCode);

    Pity lockPity(long playerId, String pityGroupKey);

    void savePity(Pity pity);

    List<PoolEntry> loadPool(long bannerId, boolean guaranteed, String guaranteedRarity);

    CreatedMercenary createMercenary(long playerId, long templateId, long releaseId, String rarity);

    long insertTransaction(
        UUID requestId,
        long playerId,
        long bannerId,
        long releaseId,
        short pullCount,
        long walletTransactionId,
        String randomTraceHash
    );

    void insertResult(
        long summonTransactionId,
        short resultNo,
        PoolEntry poolEntry,
        boolean guaranteed,
        UUID createdEntityPublicId
    );

    Optional<SummonTransaction> findByRequestId(long playerId, UUID requestId);

    record Banner(
        long bannerId,
        String bannerKey,
        long releaseId,
        String pityGroupKey,
        String currencyKey,
        long costAmount,
        short pullUnit,
        int pityThreshold,
        String guaranteedRarity
    ) {
    }

    record Pity(long playerId, String pityGroupKey, int pullCount, String guaranteedState, long version) {
    }

    record PoolEntry(long templateId, String mercenaryKey, String rarity, long weight) {
    }

    record CreatedMercenary(long mercenaryId, UUID publicId) {
    }

    record SummonTransaction(long summonTransactionId, short pullCount) {
    }
}
