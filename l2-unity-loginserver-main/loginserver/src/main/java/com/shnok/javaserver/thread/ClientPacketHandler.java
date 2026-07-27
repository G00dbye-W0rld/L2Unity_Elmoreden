package com.shnok.javaserver.thread;

import com.shnok.javaserver.db.entity.DBAccountBan;
import com.shnok.javaserver.db.entity.DBAccountInfo;
import com.shnok.javaserver.db.repository.AccountBanRepository;
import com.shnok.javaserver.db.repository.AccountInfoRepository;
import com.shnok.javaserver.dto.external.clientpackets.AuthRequestPacket;
import com.shnok.javaserver.dto.external.clientpackets.RequestServerListPacket;
import com.shnok.javaserver.dto.external.clientpackets.RequestServerLoginPacket;
import com.shnok.javaserver.dto.external.serverpackets.*;
import com.shnok.javaserver.enums.*;
import com.shnok.javaserver.enums.packettypes.external.ClientPacketType;
import com.shnok.javaserver.model.GameServerInfo;
import com.shnok.javaserver.model.SessionKey;
import com.shnok.javaserver.security.BruteForceProtector;
import com.shnok.javaserver.security.NewCrypt;
import com.shnok.javaserver.service.GameServerController;
import com.shnok.javaserver.service.LoginServerController;
import lombok.extern.log4j.Log4j2;

import javax.swing.*;
import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;
import java.net.InetAddress;
import java.security.interfaces.RSAPrivateKey;
import java.util.Arrays;
import java.util.Base64;
import java.util.Collection;

import static com.shnok.javaserver.config.Configuration.server;

@Log4j2
public class ClientPacketHandler extends Thread {
    private final LoginClientThread client;
    private final byte[] data;

    public ClientPacketHandler(LoginClientThread client, byte[] data) {
        this.client = client;
        this.data = data;
    }

    @Override
    public void run() {
        handle();
    }

    public void handle() {
        if(server.printCryptography()) {
            log.debug("<--- [CLIENT] Encrypted packet {} : {}", data.length, Arrays.toString(data));
        }

        try {
            client.getLoginCrypt().decrypt(data, 0, data.length);
        } catch (Exception e) {
            log.warn("[CLIENT] Error while decrypting client packet: ", e);
            return;
        }

        if(server.printCryptography()) {
            log.debug("<--- [CLIENT] Decrypted packet {} : {}", data.length, Arrays.toString(data));
        }

        if(!NewCrypt.verifyChecksum(data)) {
            log.warn("[CLIENT] Packet's checksum is wrong.");
            return;
        }

        ClientPacketType type = ClientPacketType.fromByte(data[0]);

        if(server.printReceivedPackets() && type != ClientPacketType.Ping) {
            log.debug("[CLIENT] Received packet: {}", type);
        }

        switch (type) {
            case Ping:
                onReceiveEcho();
                break;
            case AuthRequest:
                onReceiveAuth(client.getRSAPrivateKey());
                break;
            case RequestServerList:
                onRequestServerList();
                break;
            case RequestServerLogin:
                onRequestServerLogin();
                break;
        }
    }

    private void onReceiveEcho() {
        client.sendPacket(new PingPacket());

        Timer timer = new Timer(client.getConnectionTimeoutMs() + 100, new ActionListener() {
            @Override
            public void actionPerformed(ActionEvent arg0) {
                if (System.currentTimeMillis() - client.getLastEcho() >= client.getConnectionTimeoutMs()) {
                    log.info("User connection timeout.");
                    client.disconnect();
                }
            }
        });
        timer.setRepeats(false);
        timer.start();

        client.setLastEcho(System.currentTimeMillis(), timer);
    }

    private void onReceiveAuth(RSAPrivateKey privateKey) {
        AuthRequestPacket packet = new AuthRequestPacket(data, privateKey);
        String account = packet.getAccount();
        byte[] passHashBytes = packet.getPassHashBytes();
        String hwid = packet.getHwid();

        log.debug("Received auth for account: {}", account);

        InetAddress clientAddr = client.getConnection().getInetAddress();
        String clientIp = client.getConnectionIp();

        // Anti-bruteforce : verrouille par compte ET par IP, meme si le mot de
        // passe fourni cette fois est correct - tant que le verrou est actif,
        // aucune connexion ne passe sur cette cle.
        if (BruteForceProtector.isLocked(account) || BruteForceProtector.isLocked(clientIp)) {
            client.close(LoginFailReason.REASON_ACCESS_FAILED_TRY_AGAIN_LATER);
            return;
        }

        DBAccountInfo accountInfo;

        final String hashBase64 = Base64.getEncoder().encodeToString(passHashBytes);
        accountInfo = AccountInfoRepository.getInstance().getAccountInfo(account);

        if (accountInfo != null) {
            if(!accountInfo.getPassHash().equals(hashBase64)) {
                BruteForceProtector.recordFailure(account);
                BruteForceProtector.recordFailure(clientIp);
                client.close(LoginFailReason.REASON_USER_OR_PASS_WRONG);
                return;
            }

        } else if (server.autoCreateAccount()) {
            accountInfo = new DBAccountInfo();
            accountInfo.setLogin(account);
            accountInfo.setPassHash(hashBase64);
            accountInfo.setLastActive(System.currentTimeMillis());
            accountInfo.setLastIp(client.getConnectionIp());
            AccountInfoRepository.getInstance().createAccount(accountInfo);
            log.info("Autocreated account {}.", account);
        } else {
            BruteForceProtector.recordFailure(clientIp);
            client.close(LoginFailReason.REASON_USER_OR_PASS_WRONG);
            return;
        }

        // Identifiants corrects : on remet les compteurs d'echec a zero.
        BruteForceProtector.reset(account);
        BruteForceProtector.reset(clientIp);

        // Limite "un client lance par machine" : si ce HWID est deja actif sur un
        // AUTRE compte, on kicke l'ancienne session (ou qu'elle soit : encore sur
        // le loginserver, ou deja en jeu) et on laisse passer celle-ci - au cas ou
        // un bug empecherait la premiere session de se liberer proprement.
        if (server.hwidLimitEnabled() && (hwid != null) && !hwid.isEmpty()) {
            String previousAccount = LoginServerController.getInstance().getAccountForHwid(hwid);
            if ((previousAccount != null) && !previousAccount.equalsIgnoreCase(account)) {
                kickAccountEverywhere(previousAccount);
            }
            LoginServerController.getInstance().claimHwid(hwid, account);
            client.setHwid(hwid);
        }

        AuthLoginResult result = tryCheckinAccount(accountInfo);

        switch (result) {
            case AUTH_SUCCESS:

                client.setUsername(accountInfo.getLogin());
                client.setLoginClientState(LoginClientState.AUTHED_LOGIN);
                client.setSessionKey(LoginServerController.getInstance().getNewSessionKey());

                //Not used in ACIS interlude gameserver
                //LoginServerController.getInstance().getCharactersOnAccount(client, accountInfo.getLogin());

                // Instead bypass character count for server
                if (server.showLicense()) {
                    client.sendPacket(new LoginOkPacket(client.getSessionKey()));
                } else {
                    client.sendPacket(new ServerListPacket(client));
                }
                break;
            case INVALID_PASSWORD:
                client.close(LoginFailReason.REASON_USER_OR_PASS_WRONG);
                break;
            case ACCOUNT_INACTIVE:
                client.close(LoginFailReason.REASON_INACTIVE);
                break;
            case ACCOUNT_BANNED:
                DBAccountBan ban = AccountBanRepository.getInstance().getBan(account);
                String banReason = (ban != null) ? ban.getReason() : "";
                long banExpireDate = (ban != null) ? ban.getExpireDate() : 0;
                client.close(AccountKickedReason.REASON_PERMANENTLY_BANNED, banReason, banExpireDate);
                break;
            case ALREADY_ON_LS:
                LoginClientThread oldClient = LoginServerController.getInstance().getClient(accountInfo.getLogin());
                if (oldClient != null) {
                    // kick the other client
                    oldClient.close(LoginFailReason.REASON_ACCOUNT_IN_USE);
                }
                // kick also current client
                client.close(LoginFailReason.REASON_ACCOUNT_IN_USE);
                break;
            case ALREADY_ON_GS:
                GameServerInfo gsi = isAccountInAnyGameServer(account);
                if (gsi != null) {
                    client.close(LoginFailReason.REASON_ACCOUNT_IN_USE);

                    // kick from there
                    if (gsi.isAuthed()) {
                        gsi.getGameServerThread().kickPlayer(account);
                    }
                }
            break;
        }
    }

    public AuthLoginResult tryCheckinAccount(DBAccountInfo info) {
        if (info.getAccessLevel() < 0) {
            if (info.getAccessLevel() == server.accountInactiveLevel()) {
                return AuthLoginResult.ACCOUNT_INACTIVE;
            }

            // Verification paresseuse de l'expiration : pas de tache planifiee, on
            // ne verifie qu'au moment ou le compte essaie justement de se reconnecter.
            DBAccountBan ban = AccountBanRepository.getInstance().getBan(info.getLogin());
            if ((ban != null) && (ban.getExpireDate() != 0) && (ban.getExpireDate() <= System.currentTimeMillis())) {
                AccountInfoRepository.getInstance().updateAccessLevel(info.getLogin(), 0);
                AccountBanRepository.getInstance().deleteBan(info.getLogin());
                info.setAccessLevel(0);
                log.info("Ban expired for account {}, auto-unbanned.", info.getLogin());
            } else {
                return AuthLoginResult.ACCOUNT_BANNED;
            }
        }

        AuthLoginResult ret = AuthLoginResult.INVALID_PASSWORD;
        // check auth
        if (canCheckIn(info)) {
            // login was successful, verify presence on game servers
            ret = AuthLoginResult.ALREADY_ON_GS;
            if (isAccountInAnyGameServer(info.getLogin()) == null) {
                // account isn't on any GS verify LS itself
                ret = AuthLoginResult.ALREADY_ON_LS;

                if (LoginServerController.getInstance().getClient(info.getLogin()) == null) {
                    ret = AuthLoginResult.AUTH_SUCCESS;
                }
            }
        }

        return ret;
    }

    // Kicke un compte, ou qu'il soit actuellement : encore en cours d'auth sur le
    // loginserver, ou deja connecte a un gameserver. Utilise pour liberer un HWID
    // detenu par une autre session que celle qui vient de s'authentifier.
    private void kickAccountEverywhere(String account) {
        LoginClientThread oldClient = LoginServerController.getInstance().getClient(account);
        if (oldClient != null) {
            oldClient.close(LoginFailReason.REASON_ACCOUNT_IN_USE);
        }

        GameServerInfo gsi = isAccountInAnyGameServer(account);
        if ((gsi != null) && gsi.isAuthed()) {
            gsi.getGameServerThread().kickPlayer(account);
        }
    }

    public GameServerInfo isAccountInAnyGameServer(String account) {
        Collection<GameServerInfo> serverList = GameServerController.getInstance().getRegisteredGameServers().values();
        for (GameServerInfo gsi : serverList) {
            GameServerThread gst = gsi.getGameServerThread();
            if ((gst != null) && gst.hasAccountOnGameServer(account)) {
                return gsi;
            }
        }
        return null;
    }

    public boolean canCheckIn(DBAccountInfo info) {
        try {
            // TODO: Check at ip whitelist/ban list

            client.setAccessLevel(info.getAccessLevel());
            client.setLastGameserver(info.getLastServer());

            info.setLastIp(client.getConnectionIp());
            info.setLastActive(System.currentTimeMillis());

            AccountInfoRepository.getInstance().updateAccount(info);

            return true;
        } catch (Exception ex) {
            log.warn("There has been an error logging in!", ex);
            return false;
        }
    }

    private void onRequestServerList() {
        RequestServerListPacket packet = new RequestServerListPacket(data);

        if(client.getSessionKey().checkLoginPair(packet.getSkey1(), packet.getSkey2())) {
            log.debug("Session key verified.");
            client.sendPacket(new ServerListPacket(client));
        } else {
            client.close(LoginFailReason.REASON_ACCESS_FAILED);
        }
    }

    private void onRequestServerLogin() {
        RequestServerLoginPacket packet = new RequestServerLoginPacket(data);

        SessionKey sk = client.getSessionKey();
        // if we didn't show the license we can't check these values
        if (server.showLicense() || sk.checkLoginPair(packet.getSkey1(), packet.getSkey2())) {
            if (LoginServerController.getInstance().isLoginPossible(client, packet.getServerId())) {
                client.setJoinedGS(true);
                client.sendPacket(new PlayOkPacket(sk));
            } else {
                client.sendPacket(new PlayFailPacket(PlayFailReason.REASON_SERVER_OVERLOADED));
            }
        } else {
            client.close(LoginFailReason.REASON_ACCESS_FAILED);
        }
    }
}
