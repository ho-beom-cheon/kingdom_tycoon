package com.kingdomtycoon.server.content;

import java.util.List;
import java.util.Map;

public interface ActiveContentRepository {

    ActiveContentVersion findActive(String channelCode);

    ContentSnapshot loadSnapshot(long releaseId);

    record ActiveContentVersion(String channelCode, long releaseId, String releaseVersion, long channelVersion) {
    }

    record ContentSnapshot(
        long releaseId,
        String releaseVersion,
        Map<String, String> runtimeConfig,
        List<String> checksumRows
    ) {
        public ContentSnapshot {
            runtimeConfig = Map.copyOf(runtimeConfig);
            checksumRows = List.copyOf(checksumRows);
        }

        public String checksumMaterial() {
            return releaseVersion + "\n" + String.join("\n", checksumRows);
        }
    }
}
