package com.shnok.javaserver.security;

import java.util.concurrent.ConcurrentHashMap;

import static com.shnok.javaserver.config.Configuration.server;

// Anti-bruteforce simple, en memoire (etat transitoire, pas de table DB) : suit
// les echecs de connexion par cle sur une fenetre glissante, verrouille la cle
// le temps d'un cooldown une fois le seuil atteint. Utilise a la fois par
// compte et par IP (deux cles differentes, meme mecanisme).
public class BruteForceProtector {
    private static class Attempt {
        long windowStart;
        int count;
        long lockedUntil;
    }

    private static final ConcurrentHashMap<String, Attempt> attempts = new ConcurrentHashMap<>();

    public static boolean isLocked(String key) {
        Attempt a = attempts.get(key);
        return (a != null) && (System.currentTimeMillis() < a.lockedUntil);
    }

    public static void recordFailure(String key) {
        long now = System.currentTimeMillis();
        long windowMs = server.bruteforceWindowMinutes() * 60000L;
        long lockoutMs = server.bruteforceLockoutMinutes() * 60000L;

        Attempt a = attempts.computeIfAbsent(key, k -> new Attempt());

        synchronized (a) {
            if ((a.windowStart == 0) || (now - a.windowStart > windowMs)) {
                a.windowStart = now;
                a.count = 0;
            }
            a.count++;
            if (a.count >= server.bruteforceMaxAttempts()) {
                a.lockedUntil = now + lockoutMs;
            }
        }
    }

    public static void reset(String key) {
        attempts.remove(key);
    }
}
