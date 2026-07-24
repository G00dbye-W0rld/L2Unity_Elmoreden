CREATE TABLE IF NOT EXISTS `account_bans` (
 	`login` VARCHAR(45) NOT NULL DEFAULT '',
 	`reason` VARCHAR(255) NOT NULL DEFAULT '',
 	`banned_by` VARCHAR(45) NOT NULL DEFAULT '',
 	`ban_date` BIGINT NOT NULL DEFAULT 0,
 	`expire_date` BIGINT NOT NULL DEFAULT 0,
 	PRIMARY KEY (`login`)
);
