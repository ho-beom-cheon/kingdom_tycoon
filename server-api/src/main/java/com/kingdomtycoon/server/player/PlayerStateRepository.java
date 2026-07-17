package com.kingdomtycoon.server.player;

import java.util.UUID;

public interface PlayerStateRepository {

    PlayerStateProjection loadPlayerState(long playerId);

    SaveCheckpoint saveCheckpoint(SaveCheckpoint checkpoint);

    record PlayerStateProjection(
        long playerId,
        UUID publicId,
        String status,
        String nickname,
        int level,
        long exp,
        long profileVersion
    ) {
    }

    record SaveCheckpoint(
        long checkpointId,
        UUID publicId,
        long playerId,
        int saveVersion,
        String gameVersion,
        long contentReleaseId,
        long revision,
        String snapshotJson,
        String checksum
    ) {
        public SaveCheckpoint(
            long playerId,
            int saveVersion,
            String gameVersion,
            long contentReleaseId,
            long revision,
            String snapshotJson,
            String checksum
        ) {
            this(
                0L,
                null,
                playerId,
                saveVersion,
                gameVersion,
                contentReleaseId,
                revision,
                snapshotJson,
                checksum
            );
        }
    }
}
