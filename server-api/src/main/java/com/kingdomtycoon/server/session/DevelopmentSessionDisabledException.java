package com.kingdomtycoon.server.session;

public class DevelopmentSessionDisabledException extends RuntimeException {

    public DevelopmentSessionDisabledException() {
        super("development sessions are disabled");
    }
}
