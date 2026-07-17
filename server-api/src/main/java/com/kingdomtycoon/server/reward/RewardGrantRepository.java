package com.kingdomtycoon.server.reward;

import java.util.Optional;
import java.util.UUID;

public interface RewardGrantRepository {

    Optional<RewardGrant> findByRequestId(long playerId, UUID requestId);

    long begin(RewardGrantCommand command);

    void appendResult(long rewardGrantId, short lineNo, String rewardType, String rewardKey, long quantity);

    void complete(long rewardGrantId);

    record RewardGrant(long rewardGrantId, long playerId, UUID requestId, String status) {
    }

    record RewardGrantCommand(
        UUID requestId,
        long playerId,
        long releaseId,
        Long rewardGroupId,
        String sourceType,
        String sourceId
    ) {
    }
}
