# Serveur d'authentification Universe2D

Petit serveur separe (pas Unity) qui est le seul a parler a la base MySQL/MariaDB.
Le client Unity envoie juste un nom d'utilisateur et un mot de passe a CE serveur ;
le serveur ne renvoie jamais le mot de passe ni son hash au client, et la base n'est
jamais contactee directement par le jeu.

## 1. Creer la base et le compte technique

Avec ton client MySQL/MariaDB habituel (ligne de commande, HeidiSQL, phpMyAdmin,
MySQL Workbench...), connecte en administrateur, execute le script
`sql/001_create_accounts.sql`.

Avant de l'executer, remplace les deux `CHANGE_ME` par un mot de passe que tu
choisis pour le compte technique `universe2d_auth`.

## 2. Configurer le serveur

Dans `AuthServer/`, copie `config.example.json` vers `config.json` (ce fichier
est ignore par Git, il ne sera jamais commit) et renseigne le meme mot de passe
qu'a l'etape 1.

## 3. Verifier que le SDK .NET est installe

Dans une invite de commandes Windows :

    dotnet --version

Si la commande est introuvable, installe le SDK .NET 8 (gratuit) :
https://dotnet.microsoft.com/download/dotnet/8.0

## 4. Lancer le serveur

Dans une invite de commandes, dans le dossier `Server/AuthServer` :

    dotnet run

Tu devrais voir :
`[AuthServer] Universe2D - serveur d'authentification en ecoute sur http://localhost:8080/`

Laisse cette fenetre ouverte tant que tu veux pouvoir te connecter / creer un
compte -- c'est ce processus qui fait tourner le serveur. Ctrl+C pour l'arreter.

## 5. Tester sans Unity (recommande avant de toucher a l'ecran de connexion)

Dans une AUTRE invite de commandes, pendant que le serveur tourne :

    curl -X POST http://localhost:8080/register -H "Content-Type: application/json" -d "{\"username\":\"test\",\"password\":\"motdepasse123\"}"

Puis :

    curl -X POST http://localhost:8080/login -H "Content-Type: application/json" -d "{\"username\":\"test\",\"password\":\"motdepasse123\"}"

Et avec un mauvais mot de passe, pour verifier que c'est bien refuse :

    curl -X POST http://localhost:8080/login -H "Content-Type: application/json" -d "{\"username\":\"test\",\"password\":\"mauvais\"}"

Enfin, verifie dans la base que `password_hash` n'est jamais le mot de passe en
clair (il doit ressembler a une suite d'octets illisible) :

    SELECT username, password_hash, password_salt FROM universe2d.accounts;
