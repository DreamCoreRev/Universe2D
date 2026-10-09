-- Base de donnees et table des comptes pour Universe2D.
-- A executer une seule fois avec un client MySQL/MariaDB (ligne de commande,
-- HeidiSQL, phpMyAdmin, MySQL Workbench...), connecte en administrateur
-- (root ou equivalent).
--
-- Remplace les deux 'CHANGE_ME' par un mot de passe que TU choisis pour le
-- compte technique 'universe2d_auth' -- c'est ce meme mot de passe qu'il
-- faudra recopier dans Server/AuthServer/config.json (jamais dans Unity,
-- jamais dans Git : config.json est ignore par .gitignore).

CREATE DATABASE IF NOT EXISTS universe2d
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE universe2d;

CREATE TABLE IF NOT EXISTS accounts (
    id                   INT UNSIGNED NOT NULL AUTO_INCREMENT,
    username             VARCHAR(32)  NOT NULL,
    password_hash        VARBINARY(32) NOT NULL,
    password_salt        VARBINARY(16) NOT NULL,
    password_iterations  INT NOT NULL DEFAULT 100000,
    email                VARCHAR(255) NULL,
    created_at           DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login_at        DATETIME NULL,
    is_banned            TINYINT(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (id),
    UNIQUE KEY uq_accounts_username (username)
) ENGINE=InnoDB;

-- Compte technique dedie pour l'AuthServer : il n'a le droit de lire/ecrire
-- QUE dans cette base, jamais d'acces root -- meme si un jour ce mot de
-- passe fuit (fichier de config copie par erreur, etc.), les degats restent
-- limites a cette base.
CREATE USER IF NOT EXISTS 'universe2d_auth'@'localhost' IDENTIFIED BY 'CHANGE_ME';
GRANT SELECT, INSERT, UPDATE ON universe2d.* TO 'universe2d_auth'@'localhost';
FLUSH PRIVILEGES;
