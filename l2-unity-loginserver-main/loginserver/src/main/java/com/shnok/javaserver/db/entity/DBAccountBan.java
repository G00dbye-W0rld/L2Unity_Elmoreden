package com.shnok.javaserver.db.entity;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

import javax.persistence.*;

@Entity
@Table(name = "ACCOUNT_BANS")
@Data
@AllArgsConstructor
@NoArgsConstructor
public class DBAccountBan {
    @Id
    @Column(name = "login")
    private String login;
    @Column(name = "reason")
    private String reason;
    @Column(name = "banned_by")
    private String bannedBy;
    @Column(name = "ban_date")
    private long banDate;
    @Column(name = "expire_date")
    private long expireDate;
}
