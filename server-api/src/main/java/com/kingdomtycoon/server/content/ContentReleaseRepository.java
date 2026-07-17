package com.kingdomtycoon.server.content;

public interface ContentReleaseRepository {

    ContentRelease createDraft(
        long baseReleaseId,
        String releaseVersion,
        String releaseName,
        String minimumClientVersion,
        long actorId
    );

    int validate(long releaseId);

    void approve(long releaseId, long actorId, String checksum);

    ContentRelease find(long releaseId);

    ChannelState lockChannel(String channelCode);

    int validationErrorCount(long releaseId);

    boolean validationCompleted(long releaseId);

    void archive(long releaseId);

    void markPublished(long releaseId);

    void markRolledBack(long releaseId);

    void switchChannel(String channelCode, long fromReleaseId, long toReleaseId, long expectedVersion, long actorId);

    void appendPublishHistory(
        String channelCode,
        Long fromReleaseId,
        long toReleaseId,
        String actionType,
        String reason,
        long actorId
    );

    void appendAdminAudit(
        long actorId,
        String actionType,
        String targetId,
        String reason,
        String beforeState,
        String afterState
    );

    record ContentRelease(
        long releaseId,
        String releaseVersion,
        String status,
        String minimumClientVersion,
        Long baseReleaseId,
        String checksum
    ) {
    }

    record ChannelState(
        String channelCode,
        long activeReleaseId,
        Long previousReleaseId,
        long version
    ) {
    }
}
