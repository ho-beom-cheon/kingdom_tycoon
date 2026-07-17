SET ROLE tycoon_owner;

CREATE OR REPLACE FUNCTION master.guard_published_revision()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    old_release_status varchar(20);
    new_release_status varchar(20);
BEGIN
    -- An UPDATE must validate both sides so a row cannot be moved out of an
    -- already published release to bypass the immutability contract.
    IF TG_OP <> 'INSERT' THEN
        SELECT CASE WHEN published_at IS NOT NULL THEN 'PUBLISHED' ELSE status END
          INTO STRICT old_release_status
          FROM master.content_release
         WHERE release_id = OLD.release_id;

        IF old_release_status = 'PUBLISHED' THEN
            RAISE EXCEPTION 'PUBLISHED release % revision rows are immutable', OLD.release_id
                USING ERRCODE = '55000';
        END IF;
    END IF;

    IF TG_OP <> 'DELETE' THEN
        SELECT CASE WHEN published_at IS NOT NULL THEN 'PUBLISHED' ELSE status END
          INTO STRICT new_release_status
          FROM master.content_release
         WHERE release_id = NEW.release_id;

        IF new_release_status = 'PUBLISHED' THEN
            RAISE EXCEPTION 'PUBLISHED release % revision rows are immutable', NEW.release_id
                USING ERRCODE = '55000';
        END IF;
    END IF;

    RETURN COALESCE(NEW, OLD);
END
$function$;

CREATE OR REPLACE FUNCTION master.guard_published_summon_pool()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    target_banner_id bigint;
    target_release_id bigint;
    target_published_at timestamptz;
BEGIN
    target_banner_id := COALESCE(OLD.banner_id, NEW.banner_id);
    SELECT banner.release_id, release.published_at
      INTO STRICT target_release_id, target_published_at
      FROM ops.summon_banner banner
      JOIN master.content_release release ON release.release_id = banner.release_id
     WHERE banner.banner_id = target_banner_id;

    IF target_published_at IS NOT NULL THEN
        RAISE EXCEPTION 'PUBLISHED release % summon pool rows are immutable', target_release_id
            USING ERRCODE = '55000';
    END IF;
    RETURN COALESCE(NEW, OLD);
END
$function$;

DROP TRIGGER IF EXISTS trg_runtime_config_value__published_guard ON master.runtime_config_value;
CREATE TRIGGER trg_runtime_config_value__published_guard
    BEFORE INSERT OR UPDATE OR DELETE ON master.runtime_config_value
    FOR EACH ROW EXECUTE FUNCTION master.guard_published_revision();

DROP TRIGGER IF EXISTS trg_job_balance__published_guard ON master.job_balance;
CREATE TRIGGER trg_job_balance__published_guard
    BEFORE INSERT OR UPDATE OR DELETE ON master.job_balance
    FOR EACH ROW EXECUTE FUNCTION master.guard_published_revision();

DROP TRIGGER IF EXISTS trg_mercenary_balance__published_guard ON master.mercenary_balance;
CREATE TRIGGER trg_mercenary_balance__published_guard
    BEFORE INSERT OR UPDATE OR DELETE ON master.mercenary_balance
    FOR EACH ROW EXECUTE FUNCTION master.guard_published_revision();

DROP TRIGGER IF EXISTS trg_skill_balance__published_guard ON master.skill_balance;
CREATE TRIGGER trg_skill_balance__published_guard
    BEFORE INSERT OR UPDATE OR DELETE ON master.skill_balance
    FOR EACH ROW EXECUTE FUNCTION master.guard_published_revision();

DROP TRIGGER IF EXISTS trg_mercenary_skill_map__published_guard ON master.mercenary_skill_map;
CREATE TRIGGER trg_mercenary_skill_map__published_guard
    BEFORE INSERT OR UPDATE OR DELETE ON master.mercenary_skill_map
    FOR EACH ROW EXECUTE FUNCTION master.guard_published_revision();

DROP TRIGGER IF EXISTS trg_item_balance__published_guard ON master.item_balance;
CREATE TRIGGER trg_item_balance__published_guard
    BEFORE INSERT OR UPDATE OR DELETE ON master.item_balance
    FOR EACH ROW EXECUTE FUNCTION master.guard_published_revision();

DROP TRIGGER IF EXISTS trg_equipment_balance__published_guard ON master.equipment_balance;
CREATE TRIGGER trg_equipment_balance__published_guard
    BEFORE INSERT OR UPDATE OR DELETE ON master.equipment_balance
    FOR EACH ROW EXECUTE FUNCTION master.guard_published_revision();

DROP TRIGGER IF EXISTS trg_reward_entry__published_guard ON master.reward_entry;
CREATE TRIGGER trg_reward_entry__published_guard
    BEFORE INSERT OR UPDATE OR DELETE ON master.reward_entry
    FOR EACH ROW EXECUTE FUNCTION master.guard_published_revision();

DROP TRIGGER IF EXISTS trg_summon_banner__published_guard ON ops.summon_banner;
CREATE TRIGGER trg_summon_banner__published_guard
    BEFORE INSERT OR UPDATE OR DELETE ON ops.summon_banner
    FOR EACH ROW EXECUTE FUNCTION master.guard_published_revision();

DROP TRIGGER IF EXISTS trg_summon_pool__published_guard ON ops.summon_pool;
CREATE TRIGGER trg_summon_pool__published_guard
    BEFORE UPDATE OR DELETE ON ops.summon_pool
    FOR EACH ROW EXECUTE FUNCTION master.guard_published_summon_pool();

CREATE OR REPLACE FUNCTION master.validate_content_release(p_release_id bigint)
RETURNS integer
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = master, pg_temp
AS $function$
DECLARE
    current_status varchar(20);
    error_count integer;
BEGIN
    SELECT status INTO STRICT current_status
      FROM master.content_release
     WHERE release_id = p_release_id
     FOR UPDATE;

    IF current_status NOT IN ('DRAFT', 'VALIDATING') THEN
        RAISE EXCEPTION 'release % cannot be validated from status %', p_release_id, current_status
            USING ERRCODE = '55000';
    END IF;

    UPDATE master.content_release
       SET status = 'VALIDATING'
     WHERE release_id = p_release_id;

    DELETE FROM master.content_validation_result WHERE release_id = p_release_id;

    INSERT INTO master.content_validation_result
        (release_id, validation_code, severity, target_type, target_key, message, passed)
    SELECT
        p_release_id,
        'MISSING_RUNTIME_CONFIG',
        'ERROR',
        'RUNTIME_CONFIG',
        definition.config_key,
        '필수 runtime config 값이 없습니다.',
        false
      FROM master.runtime_config_definition definition
      LEFT JOIN master.runtime_config_value value
        ON value.release_id = p_release_id
       AND value.config_key = definition.config_key
       AND value.enabled
     WHERE value.config_key IS NULL;

    IF (SELECT count(*) FROM master.job_balance WHERE release_id = p_release_id AND enabled) <> 5 THEN
        INSERT INTO master.content_validation_result
            (release_id, validation_code, severity, target_type, message, passed)
        VALUES
            (p_release_id, 'ACTIVE_JOB_COUNT', 'ERROR', 'JOB', '활성 직업 수는 정확히 5개여야 합니다.', false);
    END IF;

    INSERT INTO master.content_validation_result
        (release_id, validation_code, severity, target_type, target_key, message, passed)
    SELECT
        p_release_id,
        'EMPTY_SUMMON_POOL',
        'ERROR',
        'SUMMON_BANNER',
        banner.banner_key,
        '활성 모집 배너의 pool 가중치 합계가 0입니다.',
        false
      FROM ops.summon_banner banner
      LEFT JOIN ops.summon_pool pool
        ON pool.banner_id = banner.banner_id
       AND pool.enabled
     WHERE banner.release_id = p_release_id
       AND banner.enabled
     GROUP BY banner.banner_id, banner.banner_key
    HAVING COALESCE(sum(pool.weight), 0) = 0;

    SELECT count(*) INTO error_count
      FROM master.content_validation_result
     WHERE release_id = p_release_id
       AND severity = 'ERROR'
       AND NOT passed;

    INSERT INTO master.content_validation_result
        (release_id, validation_code, severity, target_type, message, passed)
    VALUES
        (p_release_id, 'VALIDATION_COMPLETED', 'INFO', 'RELEASE', '콘텐츠 전체 검증을 실행했습니다.', true);

    UPDATE master.content_release
       SET status = 'DRAFT'
     WHERE release_id = p_release_id;

    RETURN error_count;
END
$function$;

REVOKE ALL ON FUNCTION master.validate_content_release(bigint) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION master.validate_content_release(bigint) TO tycoon_ops, tycoon_migrator;
