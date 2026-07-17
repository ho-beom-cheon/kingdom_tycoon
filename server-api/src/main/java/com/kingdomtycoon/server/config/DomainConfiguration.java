package com.kingdomtycoon.server.config;

import java.security.SecureRandom;
import java.util.random.RandomGenerator;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class DomainConfiguration {

    @Bean
    RandomGenerator secureRandomGenerator() {
        return new SecureRandom();
    }
}
