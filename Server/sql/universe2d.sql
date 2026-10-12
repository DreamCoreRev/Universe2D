/*
SQLyog Community v13.3.0 (64 bit)
MySQL - 10.5.8-MariaDB : Database - universe2d
*********************************************************************
*/

/*!40101 SET NAMES utf8 */;

/*!40101 SET SQL_MODE=''*/;

/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;
CREATE DATABASE /*!32312 IF NOT EXISTS*/`universe2d` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci */;

USE `universe2d`;

/*Table structure for table `accounts` */

DROP TABLE IF EXISTS `accounts`;

CREATE TABLE `accounts` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `username` varchar(32) COLLATE utf8mb4_unicode_ci NOT NULL,
  `password_hash` varbinary(32) NOT NULL,
  `password_salt` varbinary(16) NOT NULL,
  `password_iterations` int(11) NOT NULL DEFAULT 100000,
  `email` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  `last_login_at` datetime DEFAULT NULL,
  `is_banned` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_accounts_username` (`username`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

/*Data for the table `accounts` */

insert  into `accounts`(`id`,`username`,`password_hash`,`password_salt`,`password_iterations`,`email`,`created_at`,`last_login_at`,`is_banned`) values 
(1,'test','›\\Y#8‡F@Á0›≤™({TXÿ`›=5ÜÊÉç','CZ°¶å·¸Õâ11Xv=',100000,NULL,'2026-10-09 22:59:25','2026-10-09 21:01:41',0),
(2,'Aurora','lâˆÃbéDSø\'o∆~§soù‘öcT5àÂyŒF~','D3/«è“%∞\nã/˛',100000,NULL,'2026-10-09 23:18:23','2026-10-11 13:56:58',0),
(4,'Naruto','Mÿ/+®…<oï4>Z†∂…Œ”oÕØf∆Ù“Eiiîº“Øà','8◊(g8È¡\nóè #',100000,NULL,'2026-10-09 23:29:08','2026-10-11 13:27:35',0),
(5,'Slytherin','ÙL˝8êËÿËßÖYmÌ“ø@à!esvf¢ZÏˇ∏*r','ˇ œœ¡ÉûiÌmª*ô',100000,NULL,'2026-10-10 04:11:09','2026-10-11 00:39:36',0);

/*Table structure for table `characters` */

DROP TABLE IF EXISTS `characters`;

CREATE TABLE `characters` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `account_id` int(10) unsigned NOT NULL,
  `name` varchar(24) COLLATE utf8mb4_unicode_ci NOT NULL,
  `class` varchar(16) COLLATE utf8mb4_unicode_ci NOT NULL,
  `save_slot_index` tinyint(3) unsigned NOT NULL,
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  `last_played_at` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_characters_account_name` (`account_id`,`name`),
  UNIQUE KEY `uq_characters_account_slot` (`account_id`,`save_slot_index`),
  CONSTRAINT `fk_characters_account` FOREIGN KEY (`account_id`) REFERENCES `accounts` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=20 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

/*Data for the table `characters` */

insert  into `characters`(`id`,`account_id`,`name`,`class`,`save_slot_index`,`created_at`,`last_played_at`) values 
(10,4,'Naruto','Mage',0,'2026-10-11 00:42:50',NULL),
(14,4,'Naruu','Guerrier',1,'2026-10-11 02:29:38',NULL),
(15,5,'Slytherin','Mage',0,'2026-10-11 02:40:01',NULL),
(16,5,'Slyth','Guerrier',1,'2026-10-11 02:40:46',NULL),
(18,2,'Aurora','Mage',0,'2026-10-11 04:06:55',NULL),
(19,2,'Nehy','Guerrier',1,'2026-10-11 04:07:12',NULL);

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;
