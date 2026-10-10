-- Table des personnages pour Universe2D.
-- A executer avec le meme client MySQL/MariaDB que 001_create_accounts.sql,
-- connecte en administrateur, APRES avoir deja execute 001_create_accounts.sql.
--
-- Chaque personnage appartient a un compte (account_id) et pointe vers un
-- "slot" de sauvegarde LOCAL (save_slot_index, 0 a 4) : la progression du
-- personnage (niveau, XP, position, equipement, inventaire...) continue
-- d'etre sauvegardee en local par le systeme existant (SaveManager.cs,
-- fichiers .dat) -- cette table ne sert qu'a savoir, par compte, quels
-- personnages existent (nom + classe) et quel slot local chacun utilise,
-- pour pouvoir les lister sur l'ecran de selection, meme apres reconnexion.

USE universe2d;

CREATE TABLE IF NOT EXISTS characters (
    id                INT UNSIGNED NOT NULL AUTO_INCREMENT,
    account_id        INT UNSIGNED NOT NULL,
    name              VARCHAR(24)  NOT NULL,
    class             VARCHAR(16)  NOT NULL,
    save_slot_index   TINYINT UNSIGNED NOT NULL,
    created_at        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_played_at    DATETIME NULL,
    PRIMARY KEY (id),
    CONSTRAINT fk_characters_account FOREIGN KEY (account_id)
        REFERENCES accounts (id) ON DELETE CASCADE,
    -- Un compte ne peut pas avoir deux personnages avec le meme nom, ni
    -- utiliser le meme slot local deux fois (slots 0 a 4 => max 5 persos).
    UNIQUE KEY uq_characters_account_name (account_id, name),
    UNIQUE KEY uq_characters_account_slot (account_id, save_slot_index)
) ENGINE=InnoDB;

-- Le compte technique 'universe2d_auth' a deja SELECT/INSERT/UPDATE sur
-- toute la base universe2d.* (accorde dans 001_create_accounts.sql) -- il
-- lui manque juste DELETE pour pouvoir supprimer un personnage.
GRANT DELETE ON universe2d.* TO 'universe2d_auth'@'localhost';
FLUSH PRIVILEGES;
