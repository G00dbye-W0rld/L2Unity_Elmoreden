package com.shnok.javaserver.db.repository;

import com.shnok.javaserver.db.DbFactory;
import com.shnok.javaserver.db.entity.DBAccountBan;
import com.shnok.javaserver.db.interfaces.AccountBanDao;
import lombok.extern.log4j.Log4j2;
import org.hibernate.Session;

@Log4j2
public class AccountBanRepository implements AccountBanDao {
    private static AccountBanRepository instance;
    public static AccountBanRepository getInstance() {
        if (instance == null) {
            instance = new AccountBanRepository();
        }
        return instance;
    }

    @Override
    public DBAccountBan getBan(String login) {
        try (Session session = DbFactory.getSessionFactory().openSession()) {
            return session.createQuery("SELECT i FROM DBAccountBan i WHERE login=:login", DBAccountBan.class)
                    .setParameter("login", login)
                    .getSingleResult();
        } catch (Exception e) {
            log.warn(e.getMessage());
            return null;
        }
    }

    @Override
    public void deleteBan(String login) {
        try (Session session = DbFactory.getSessionFactory().openSession()) {
            session.beginTransaction();
            session.createQuery("DELETE FROM DBAccountBan i WHERE login=:login")
                    .setParameter("login", login)
                    .executeUpdate();
            session.getTransaction().commit();
        } catch (Exception e) {
            log.error("SQL ERROR: {}", e.getMessage(), e);
        }
    }
}
