package com.kingdomtycoon.server.content;

public interface MasterDataProvider {

    long activeReleaseId();

    String getConfig(String configKey);

    JobDefinition getJob(String jobKey);

    MercenaryDefinition getMercenary(String mercenaryKey);

    ItemDefinition getItem(String itemKey);

    EquipmentDefinition getEquipment(String equipmentKey);

    record JobDefinition(String jobKey, long baseHp, long baseAttack, long baseDefense) {
    }

    record MercenaryDefinition(String mercenaryKey, String jobKey, String rarity, boolean summonable) {
    }

    record ItemDefinition(String itemKey, String itemType, long maxStack) {
    }

    record EquipmentDefinition(String equipmentKey, String equipmentSlot, String grade, String statCode) {
    }
}
