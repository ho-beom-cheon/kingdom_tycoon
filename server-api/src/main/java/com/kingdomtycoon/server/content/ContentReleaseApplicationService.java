package com.kingdomtycoon.server.content;

import com.kingdomtycoon.server.outbox.OutboxRepository;
import com.kingdomtycoon.server.support.RequestHash;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class ContentReleaseApplicationService {

    private final ContentReleaseRepository contentReleaseRepository;
    private final ActiveContentRepository activeContentRepository;
    private final OutboxRepository outboxRepository;

    public ContentReleaseApplicationService(
        ContentReleaseRepository contentReleaseRepository,
        ActiveContentRepository activeContentRepository,
        OutboxRepository outboxRepository
    ) {
        this.contentReleaseRepository = contentReleaseRepository;
        this.activeContentRepository = activeContentRepository;
        this.outboxRepository = outboxRepository;
    }

    @Transactional
    public ContentReleaseRepository.ContentRelease createDraft(
        long baseReleaseId,
        String releaseVersion,
        String releaseName,
        String minimumClientVersion,
        long actorId
    ) {
        return contentReleaseRepository.createDraft(
            baseReleaseId,
            releaseVersion,
            releaseName,
            minimumClientVersion,
            actorId
        );
    }

    @Transactional
    public int validate(long releaseId) {
        return contentReleaseRepository.validate(releaseId);
    }

    @Transactional
    public void approve(long releaseId, long actorId) {
        if (!contentReleaseRepository.validationCompleted(releaseId)) {
            throw new IllegalStateException("release must be validated before approval");
        }
        if (contentReleaseRepository.validationErrorCount(releaseId) > 0) {
            throw new IllegalStateException("release has validation errors");
        }
        ActiveContentRepository.ContentSnapshot snapshot = activeContentRepository.loadSnapshot(releaseId);
        String checksum = RequestHash.sha256(snapshot.checksumMaterial());
        contentReleaseRepository.approve(releaseId, actorId, checksum);
    }

    @Transactional
    public PublishResult publish(
        String channelCode,
        long releaseId,
        long expectedChannelVersion,
        long actorId,
        String reason
    ) {
        requireReason(reason);
        ContentReleaseRepository.ChannelState channel = contentReleaseRepository.lockChannel(channelCode);
        if (channel.version() != expectedChannelVersion) {
            throw new IllegalStateException("content channel version mismatch");
        }
        ContentReleaseRepository.ContentRelease release = contentReleaseRepository.find(releaseId);
        if (!("APPROVED".equals(release.status()) || "SCHEDULED".equals(release.status()))) {
            throw new IllegalStateException("release must be APPROVED or SCHEDULED");
        }
        if (contentReleaseRepository.validationErrorCount(releaseId) > 0) {
            throw new IllegalStateException("release has validation errors");
        }

        long fromReleaseId = channel.activeReleaseId();
        contentReleaseRepository.archive(fromReleaseId);
        contentReleaseRepository.markPublished(releaseId);
        contentReleaseRepository.switchChannel(
            channelCode,
            fromReleaseId,
            releaseId,
            expectedChannelVersion,
            actorId
        );
        contentReleaseRepository.appendPublishHistory(
            channelCode,
            fromReleaseId,
            releaseId,
            "PUBLISH",
            reason,
            actorId
        );
        contentReleaseRepository.appendAdminAudit(
            actorId,
            "CONTENT_PUBLISH",
            channelCode,
            reason,
            "{\"releaseId\":%d,\"version\":%d}".formatted(fromReleaseId, expectedChannelVersion),
            "{\"releaseId\":%d,\"version\":%d}".formatted(releaseId, expectedChannelVersion + 1)
        );
        outboxRepository.append(
            "CONTENT_CHANNEL",
            channelCode,
            "ContentReleasePublished",
            "{\"channelCode\":\"%s\",\"releaseId\":%d}".formatted(channelCode, releaseId)
        );
        return new PublishResult(channelCode, fromReleaseId, releaseId, expectedChannelVersion + 1);
    }

    @Transactional
    public PublishResult rollback(
        String channelCode,
        long targetReleaseId,
        long expectedChannelVersion,
        long actorId,
        String reason
    ) {
        requireReason(reason);
        ContentReleaseRepository.ChannelState channel = contentReleaseRepository.lockChannel(channelCode);
        if (channel.version() != expectedChannelVersion) {
            throw new IllegalStateException("content channel version mismatch");
        }
        if (channel.activeReleaseId() == targetReleaseId) {
            throw new IllegalArgumentException("target release is already active");
        }
        ContentReleaseRepository.ContentRelease target = contentReleaseRepository.find(targetReleaseId);
        if (target.checksum() == null || target.checksum().isBlank()) {
            throw new IllegalStateException("rollback target was never approved");
        }

        long fromReleaseId = channel.activeReleaseId();
        contentReleaseRepository.markRolledBack(fromReleaseId);
        contentReleaseRepository.markPublished(targetReleaseId);
        contentReleaseRepository.switchChannel(
            channelCode,
            fromReleaseId,
            targetReleaseId,
            expectedChannelVersion,
            actorId
        );
        contentReleaseRepository.appendPublishHistory(
            channelCode,
            fromReleaseId,
            targetReleaseId,
            "ROLLBACK",
            reason,
            actorId
        );
        contentReleaseRepository.appendAdminAudit(
            actorId,
            "CONTENT_ROLLBACK",
            channelCode,
            reason,
            "{\"releaseId\":%d,\"version\":%d}".formatted(fromReleaseId, expectedChannelVersion),
            "{\"releaseId\":%d,\"version\":%d}".formatted(targetReleaseId, expectedChannelVersion + 1)
        );
        outboxRepository.append(
            "CONTENT_CHANNEL",
            channelCode,
            "ContentReleaseRolledBack",
            "{\"channelCode\":\"%s\",\"releaseId\":%d}".formatted(channelCode, targetReleaseId)
        );
        return new PublishResult(channelCode, fromReleaseId, targetReleaseId, expectedChannelVersion + 1);
    }

    private static void requireReason(String reason) {
        if (reason == null || reason.isBlank()) {
            throw new IllegalArgumentException("reason is required");
        }
    }

    public record PublishResult(
        String channelCode,
        long fromReleaseId,
        long toReleaseId,
        long channelVersion
    ) {
    }
}
