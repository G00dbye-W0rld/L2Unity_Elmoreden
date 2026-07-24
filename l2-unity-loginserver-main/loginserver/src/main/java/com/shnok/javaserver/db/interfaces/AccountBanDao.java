package com.shnok.javaserver.db.interfaces;

import com.shnok.javaserver.db.entity.DBAccountBan;

public interface AccountBanDao {
    DBAccountBan getBan(String login);
    void deleteBan(String login);
}
