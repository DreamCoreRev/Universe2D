<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Eleona – Télécharger le jeu</title>
<meta name="description" content="Eleona, un MMORPG chibi à explorer entre amis. Télécharge-le sur PC, Android et iOS.">
<meta property="og:title" content="Eleona – MMORPG chibi sur PC et mobile">
<meta property="og:description" content="Explore plusieurs mondes, forme un groupe et pars à l'aventure.">
<meta property="og:image" content="assets/YsJzGX.png">
<link rel="icon" href="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'%3E%3Crect width='64' height='64' rx='16' fill='%236cc51c'/%3E%3Ctext x='32' y='46' font-size='40' font-family='Arial Black' text-anchor='middle' fill='white'%3EE%3C/text%3E%3C/svg%3E">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Lilita+One&family=Nunito:wght@500;700;800&display=swap" rel="stylesheet">
<style>
:root{
  --ink:#1c2133;
  --ink-2:#262c44;
  --paper:#ffffff;
  --muted:#b9c0d9;
  --gold:#f5b82e;
  --plume:#e0433b;
  --ground:#6cc51c;
  --ground-2:#5db315;
  --tuft:#4a9a10;
  --accent:#6cc51c;
  --accent-ink:#17330a;
  --r:22px;
  --display:"Lilita One","Arial Rounded MT Bold",system-ui,sans-serif;
  --body:"Nunito",system-ui,sans-serif;
}
/* Un thème par monde : seules ces variables changent. */
body[data-world="prairies"]{--ground:#6cc51c;--ground-2:#5db315;--tuft:#4a9a10;--accent:#6cc51c;--accent-ink:#17330a}
body[data-world="dunes"]   {--ground:#e9b85a;--ground-2:#dca847;--tuft:#c08a2c;--accent:#f5b82e;--accent-ink:#3a2704}
body[data-world="glaces"]  {--ground:#bfe6f5;--ground-2:#a9d9ee;--tuft:#86c1dc;--accent:#7fd0f2;--accent-ink:#0b2f40}
body[data-world="marais"]  {--ground:#7a5cb0;--ground-2:#6c4ea3;--tuft:#523a85;--accent:#a07ee0;--accent-ink:#1e1236}
body[data-world="volcan"]  {--ground:#d2543a;--ground-2:#c24730;--tuft:#9c3220;--accent:#ff8a4c;--accent-ink:#3d1205}

*{box-sizing:border-box}
html{scroll-behavior:smooth}
body{margin:0;background:var(--ink);color:var(--paper);font:500 18px/1.55 var(--body);-webkit-font-smoothing:antialiased}
a{color:inherit}
:focus-visible{outline:3px solid var(--gold);outline-offset:3px;border-radius:10px}
.wrap{width:min(1100px,100% - 32px);margin-inline:auto}

/* ——— Hero ——— */
.hero{position:relative;overflow:hidden;padding:56px 0 0;min-height:min(760px,100svh)}
.hero .wrap{position:relative;z-index:2}
.logo{
  font:400 clamp(72px,16vw,176px)/.9 var(--display);margin:0;letter-spacing:.01em;color:#fff;
  -webkit-text-stroke:0;
  text-shadow:
    0 6px 0 #0d1020,
    -4px 0 0 #0d1020, 4px 0 0 #0d1020, 0 -4px 0 #0d1020,
    -4px 4px 0 #0d1020, 4px 4px 0 #0d1020, -4px -4px 0 #0d1020, 4px -4px 0 #0d1020,
    0 14px 0 rgba(0,0,0,.25);
}
.tagline{max-width:30ch;margin:22px 0 28px;font-size:clamp(20px,2.6vw,26px);line-height:1.35;font-weight:700}
.platforms{display:flex;flex-wrap:wrap;gap:12px;margin:0 0 20px;padding:0;list-style:none}
.btn{
  display:inline-flex;align-items:center;gap:12px;padding:12px 22px 12px 16px;border-radius:16px;
  background:var(--accent);color:var(--accent-ink);text-decoration:none;font-weight:800;line-height:1.1;
  box-shadow:0 5px 0 rgba(0,0,0,.35);transition:transform .12s ease, box-shadow .12s ease, background-color .3s;
}
.btn small{display:block;font-weight:700;font-size:13px;opacity:.8}
.btn svg{width:28px;height:28px;flex:none}
.btn:hover{transform:translateY(-2px);box-shadow:0 7px 0 rgba(0,0,0,.35)}
.btn:active{transform:translateY(3px);box-shadow:0 2px 0 rgba(0,0,0,.35)}
.btn.alt{background:#fff;color:#1c2133}
.note{margin:0;color:var(--muted);font-size:15px}

.hero-art{position:absolute;top:64px;right:max(16px,calc((100% - 1100px) / 2));width:min(38vw,420px);height:auto;z-index:1}

/* Sol en tuiles, il change avec le monde */
.ground{
  position:relative;margin-top:56px;padding:34px 0 44px;
  background-color:var(--ground);
  background-image:
    radial-gradient(circle at 30% 40%, var(--tuft) 0 3px, transparent 4px),
    radial-gradient(circle at 68% 62%, var(--tuft) 0 2.5px, transparent 3.5px),
    linear-gradient(var(--ground-2) 0 0);
  background-size:72px 72px,72px 72px,100% 100%;
  box-shadow:inset 0 10px 0 rgba(0,0,0,.18);
  transition:background-color .35s ease;
}
.ground::before{ /* joints des tuiles */
  content:"";position:absolute;inset:0;pointer-events:none;
  background:
    linear-gradient(90deg, rgba(0,0,0,.1) 0 3px, transparent 3px) 0 0/72px 72px,
    linear-gradient(0deg,  rgba(0,0,0,.1) 0 3px, transparent 3px) 0 0/72px 72px;
}
.ground .wrap{position:relative}
.picker-title{margin:0 0 14px;font:400 28px/1.1 var(--display);color:#fff;text-shadow:-2px 0 #0d1020,2px 0 #0d1020,0 -2px #0d1020,0 2px #0d1020,-2px -2px #0d1020,2px 2px #0d1020,-2px 2px #0d1020,2px -2px #0d1020,0 4px #0d1020}
.worlds{display:flex;flex-wrap:wrap;gap:10px;margin:0;padding:0;list-style:none}
.worlds button{
  font:800 16px/1 var(--body);padding:12px 18px;border-radius:999px;border:3px solid #0d1020;
  background:#fff;color:#1c2133;cursor:pointer;box-shadow:0 4px 0 #0d1020;transition:transform .1s ease;
}
.worlds button:hover{transform:translateY(-2px)}
.worlds button[aria-pressed="true"]{background:var(--gold);transform:translateY(2px);box-shadow:0 2px 0 #0d1020}
.world-line{display:block;width:fit-content;min-height:3.2em;margin:18px 0 0;max-width:46ch;padding:12px 16px;border-radius:14px;background:#0d1020;font-weight:800;color:#fff}

/* ——— Sections ——— */
section.block{padding:84px 0 0}
h2{font:400 clamp(32px,5vw,48px)/1.05 var(--display);margin:0 0 28px;letter-spacing:.01em}
.features{display:grid;grid-template-columns:repeat(3,1fr);gap:22px;margin:0;padding:0;list-style:none}
.features li{padding:26px;border-radius:var(--r);background:var(--ink-2);border:3px solid #0d1020;box-shadow:0 6px 0 #0d1020}
.features h3{margin:0 0 8px;font:400 26px/1.1 var(--display);color:var(--gold)}
.features p{margin:0;color:var(--muted)}

.shots{display:grid;grid-template-columns:repeat(3,1fr);gap:18px;margin:0;padding:0;list-style:none}
.shots figure{margin:0}
.shots .frame{
  aspect-ratio:16/10;border-radius:var(--r);overflow:hidden;border:3px solid #0d1020;box-shadow:0 6px 0 #0d1020;
  position:relative;background:linear-gradient(135deg,var(--ink-2),#2f3756);display:grid;place-items:center;color:var(--muted);font-weight:800;text-align:center;padding:12px
}
.shots img{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;display:block}
.shots figcaption{margin-top:12px;color:var(--muted);font-size:15px}

.cta{margin:96px 0 0;padding:64px 0;background:var(--ink-2);border-block:3px solid #0d1020}
.cta h2{margin-bottom:20px}

footer{padding:36px 0 48px;color:var(--muted);font-size:15px}
footer .wrap{display:flex;flex-wrap:wrap;gap:12px 28px;justify-content:space-between}
footer ul{display:flex;gap:20px;margin:0;padding:0;list-style:none}

@media (max-width:860px){
  .hero-art{position:static;display:block;width:min(80%,360px);margin:24px 0 0 max(16px,calc((100% - 1100px) / 2))}
  .features,.shots{grid-template-columns:1fr}
  .hero{min-height:0}
}
@media (prefers-reduced-motion:no-preference){
  .logo{animation:pop .7s cubic-bezier(.2,1.4,.4,1) both}
  @keyframes pop{from{transform:translateY(14px) scale(.96);opacity:0}to{transform:none;opacity:1}}
}
@media (prefers-reduced-motion:reduce){*{transition:none!important}}
</style>
</head>
<body data-world="prairies">

<header class="hero">
  <div class="wrap">
    <h1 class="logo">Eleona</h1>
    <p class="tagline">Un MMORPG chibi à explorer entre amis, sur PC et sur mobile.</p>

    <!-- À REMPLACER : les liens de téléchargement (voir la liste dans le message) -->
    <ul class="platforms" id="telecharger">
      <li><a class="btn" href="downloads/Windows/Eleona.rar" download>
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M3 5.5 11 4.4v7H3zM12 4.2 21 3v8.4h-9zM3 12.6h8v7l-8-1.1zM12 12.6h9V21l-9-1.2z"/></svg>
        <span><small>Télécharger pour</small>Windows</span></a></li>
      <li><a class="btn alt" href="downloads/Android/Eleona.apk" rel="noopener">
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M4 3.5v17a1 1 0 0 0 1.5.9l14-8.5a1 1 0 0 0 0-1.8l-14-8.5A1 1 0 0 0 4 3.5z"/></svg>
        <span><small>Disponible sur</small>Google Play</span></a></li>
      <li><a class="btn alt" href="downloads/Android/Eleona.ipa" rel="noopener">
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M16.4 12.6c0-2.3 1.9-3.4 2-3.5-1.1-1.6-2.8-1.8-3.4-1.8-1.4-.2-2.8.8-3.5.8s-1.8-.8-3-.8c-1.6 0-3 .9-3.8 2.3-1.6 2.8-.4 7 1.2 9.3.8 1.1 1.7 2.4 2.9 2.3 1.2 0 1.6-.7 3-.7s1.8.7 3 .7 2-1.1 2.8-2.2c.9-1.3 1.2-2.5 1.3-2.6-.1 0-2.5-1-2.5-3.800zM14.200 5.800c.6-.8 1.1-1.8.9-2.8-.9 0-2 .6-2.6 1.400-.6.700-1.1 1.7-.9 2.700 1 .1 2-.5 2.6-1.3z"/></svg>
        <span><small>Télécharger sur</small>App Store</span></a></li>
    </ul>
    <p class="note">Gratuit. Un compte suffit pour jouer sur tous tes appareils.</p>
  </div>

  <img class="hero-art" src="assets/YsJzGX.png" alt="Un mage à barbe blanche et un chevalier au casque à plume rouge" width="630" height="500">

  <div class="ground">
    <div class="wrap">
      <h2 class="picker-title">Choisis un monde</h2>
      <ul class="worlds" role="list">
        <li><button type="button" data-world="prairies" aria-pressed="true">Prairies</button></li>
        <li><button type="button" data-world="dunes" aria-pressed="false">Dunes</button></li>
        <li><button type="button" data-world="glaces" aria-pressed="false">Terres glacées</button></li>
        <li><button type="button" data-world="marais" aria-pressed="false">Marais</button></li>
        <li><button type="button" data-world="volcan" aria-pressed="false">Volcan</button></li>
      </ul>
      <p class="world-line" id="world-line" aria-live="polite"></p>
    </div>
  </div>
</header>

<main>
  <section class="block wrap" aria-labelledby="t-jeu">
    <h2 id="t-jeu">Ce que tu fais dans Eleona</h2>
    <ul class="features">
      <li><h3>Joue en groupe</h3><p>Invite des amis, forme un groupe et discute en direct pendant que vous partez à l'aventure.</p></li>
      <li><h3>Progresse à ta façon</h3><p>Lance des sorts, monte de niveau, apprends un métier et choisis tes talents.</p></li>
      <li><h3>Change de décor</h3><p>Prairies, dunes, glace, marais… chaque monde a ses paysages et ses monstres.</p></li>
    </ul>
  </section>

  <section class="block wrap" aria-labelledby="t-img">
    <h2 id="t-img">En images</h2>
    <!-- Ajoute tes captures dans assets/ : screen1.jpg, screen2.jpg, screen3.jpg -->
    <ul class="shots">
      <li><figure><div class="frame"><img src="assets/iaIfsr.gif" alt="Un mage au milieu d'une prairie" onerror="this.replaceWith(document.createTextNode('Capture à ajouter : assets/screen1.jpg'))"></div><figcaption>Les prairies</figcaption></figure></li>
      <li><figure><div class="frame"><img src="assets/ZcW3HH.gif" alt="Combat contre un squelette" onerror="this.replaceWith(document.createTextNode('Capture à ajouter : assets/screen2.jpg'))"></div><figcaption>Les combats</figcaption></figure></li>
      <li><figure><div class="frame"><img src="assets/LSEXQL.gif" alt="Inventaire du personnage" onerror="this.replaceWith(document.createTextNode('Capture à ajouter : assets/screen3.jpg'))"></div><figcaption>L'inventaire et l'équipement</figcaption></figure></li>
    </ul>
  </section>

  <section class="cta">
    <div class="wrap">
      <h2>Prêt à partir ?</h2>
      <a class="btn" href="#telecharger">Choisir ma plateforme</a>
    </div>
  </section>
</main>

<footer>
  <div class="wrap">
    <span>© 2026 Eleona</span>
    <ul>
      <li><a href="mailto:contact@eleona.eu">contact@eleona.eu</a></li>
      <li><a href="#" rel="noopener">Discord</a></li>
      <li><a href="#">Mentions légales</a></li>
    </ul>
  </div>
</footer>

<script>
(function(){
  var lines = {
    prairies:"Des champs, des pommiers et un étang : un premier pas tranquille… jusqu'au premier squelette.",
    dunes:"Du sable, du soleil et des ruines à fouiller. Pense à emporter de l'eau.",
    glaces:"Neige épaisse, vent glacé et des monstres qui n'aiment pas les visiteurs.",
    marais:"Brume, lanternes et des racines qui bougent quand tu ne regardes pas.",
    volcan:"De la lave, des cendres et un équipement à faire briller."
  };
  var line = document.getElementById("world-line");
  var btns = document.querySelectorAll(".worlds button");
  function set(w){
    document.body.setAttribute("data-world", w);
    btns.forEach(function(b){ b.setAttribute("aria-pressed", String(b.dataset.world === w)); });
    line.textContent = lines[w];
    try{ localStorage.setItem("eleona-world", w); }catch(e){}
  }
  btns.forEach(function(b){ b.addEventListener("click", function(){ set(b.dataset.world); }); });
  set("prairies");
})();
</script>
</body>
</html>
