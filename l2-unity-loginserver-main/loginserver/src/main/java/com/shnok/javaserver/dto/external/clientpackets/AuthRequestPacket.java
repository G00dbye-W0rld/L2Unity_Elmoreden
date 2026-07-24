package com.shnok.javaserver.dto.external.clientpackets;

import com.shnok.javaserver.dto.ReceivablePacket;
import com.shnok.javaserver.util.HexUtils;
import lombok.Getter;
import lombok.extern.log4j.Log4j2;

import javax.crypto.Cipher;
import java.security.interfaces.RSAPrivateKey;
import java.util.Arrays;

import static com.shnok.javaserver.config.Configuration.server;

@Getter
@Log4j2
public class AuthRequestPacket extends ReceivablePacket {
    private final byte[] raw = new byte[128];
    private String account;
    private byte[] passHashBytes;
    private String hwid = "";

    public AuthRequestPacket(byte[] data, RSAPrivateKey privateKey) {
        super(data);

        byte[] decrypted;

        try {
            final Cipher rsaCipher = Cipher.getInstance(server.clientRsaPaddingMode());
            rsaCipher.init(Cipher.DECRYPT_MODE, privateKey);

            decrypted = Arrays.copyOfRange(data, 1,  1 + 128);

            if(server.printCryptography()) {
                log.debug("Encrypted client RSA: {}", Arrays.toString(decrypted));
            }

            decrypted = rsaCipher.doFinal(decrypted, 0x00, 0x80);

            if(server.printCryptography()) {
                log.debug("Decrypted client RSA: {}", Arrays.toString(decrypted));
            }
        } catch (Exception ex) {
            log.warn("There has been an error trying to login!", ex);
            return;
        }

        int accountBlockLength = decrypted[0];
        try {
            account = new String(decrypted, 1, accountBlockLength).trim().toLowerCase();
            passHashBytes = Arrays.copyOfRange(decrypted, accountBlockLength + 2, decrypted.length);
            if(server.printCryptography()) {
                log.debug("Password hash: {}", HexUtils.hexToString(passHashBytes));
            }
        } catch (Exception ex) {
            log.warn("There has been an error parsing credentials!", ex);
        }

        // Identifiant machine, ajoute apres le bloc RSA de 128 octets. L'iterateur
        // de base reste bloque a 1 (le bloc RSA a ete copie a la main plus haut,
        // pas via l'iterateur) - on l'avance donc explicitement jusqu'a 129 avant
        // de lire la chaine.
        try {
            readB(128);
            hwid = readS();
        } catch (Exception ex) {
            hwid = "";
        }
    }
}
