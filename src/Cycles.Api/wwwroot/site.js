const canvas = document.querySelector("#heroScene");
const context = canvas.getContext("2d");
const reducedMotionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
const scene = {
    width: 0,
    height: 0,
    stars: [],
    systems: [],
    time: 0
};
let animationFrame = null;
let resizeFrame = null;
let lastFrameTime = null;

const palette = {
    text: "rgba(243, 241, 232, 0.78)",
    muted: "rgba(184, 193, 180, 0.44)",
    green: "rgba(142, 216, 188, 0.9)",
    gold: "rgba(225, 189, 103, 0.88)",
    red: "rgba(216, 100, 85, 0.78)"
};

function resize() {
    const ratio = Math.min(window.devicePixelRatio || 1, 2);
    scene.width = window.innerWidth;
    scene.height = window.innerHeight;
    canvas.width = Math.floor(scene.width * ratio);
    canvas.height = Math.floor(scene.height * ratio);
    canvas.style.width = `${scene.width}px`;
    canvas.style.height = `${scene.height}px`;
    context.setTransform(ratio, 0, 0, ratio, 0, 0);
    seedScene();
    renderScene();
}

function seedScene() {
    scene.stars = Array.from({ length: Math.min(180, Math.floor(scene.width / 8)) }, (_, index) => ({
        x: pseudo(index * 31 + 7) * scene.width,
        y: pseudo(index * 53 + 11) * scene.height,
        size: 0.5 + pseudo(index * 97 + 3) * 1.5,
        alpha: 0.12 + pseudo(index * 19 + 5) * 0.34
    }));

    const base = [
        [0.63, 0.23, "Pseudopolis", palette.green],
        [0.78, 0.42, "Treaty Gate", palette.gold],
        [0.58, 0.62, "Archive Nine", palette.text],
        [0.84, 0.70, "Red Meridian", palette.red],
        [0.44, 0.38, "Silent Loom", palette.text]
    ];

    scene.systems = base.map(([x, y, name, colour], index) => ({
        x: x * scene.width,
        y: y * scene.height,
        name,
        colour,
        radius: 5 + index
    }));
}

function renderScene() {
    context.clearRect(0, 0, scene.width, scene.height);
    context.fillStyle = "#090d0b";
    context.fillRect(0, 0, scene.width, scene.height);

    drawGrid();
    drawStars();
    drawRoutes();
    drawSystems();
}

function animate(frameTime) {
    const elapsed = lastFrameTime === null ? 0 : Math.min(frameTime - lastFrameTime, 50);
    lastFrameTime = frameTime;
    scene.time += elapsed * 0.00036;
    renderScene();

    animationFrame = requestAnimationFrame(animate);
}

function stopAnimation() {
    if (animationFrame !== null) {
        cancelAnimationFrame(animationFrame);
        animationFrame = null;
    }

    lastFrameTime = null;
}

function syncAnimation() {
    if (reducedMotionQuery.matches || document.hidden) {
        stopAnimation();
        if (reducedMotionQuery.matches) {
            scene.time = 0;
        }

        renderScene();
        return;
    }

    if (animationFrame === null) {
        animationFrame = requestAnimationFrame(animate);
    }
}

function scheduleResize() {
    if (resizeFrame !== null) {
        return;
    }

    resizeFrame = requestAnimationFrame(() => {
        resizeFrame = null;
        resize();
    });
}

function drawGrid() {
    context.save();
    context.globalAlpha = 0.16;
    context.strokeStyle = "rgba(243, 241, 232, 0.16)";
    context.lineWidth = 1;
    const step = 52;
    const offset = (scene.time * 18) % step;

    for (let x = -step + offset; x < scene.width + step; x += step) {
        context.beginPath();
        context.moveTo(x, 0);
        context.lineTo(x + scene.height * 0.18, scene.height);
        context.stroke();
    }

    for (let y = -step; y < scene.height + step; y += step) {
        context.beginPath();
        context.moveTo(0, y + offset);
        context.lineTo(scene.width, y - scene.width * 0.08 + offset);
        context.stroke();
    }

    context.restore();
}

function drawStars() {
    for (const star of scene.stars) {
        context.fillStyle = `rgba(243, 241, 232, ${star.alpha})`;
        context.beginPath();
        context.arc(star.x, star.y, star.size, 0, Math.PI * 2);
        context.fill();
    }
}

function drawRoutes() {
    context.save();
    context.strokeStyle = palette.muted;
    context.lineWidth = 2;
    for (let index = 0; index < scene.systems.length - 1; index++) {
        const a = scene.systems[index];
        const b = scene.systems[index + 1];
        context.beginPath();
        context.moveTo(a.x, a.y);
        context.lineTo(b.x, b.y);
        context.stroke();
    }
    context.restore();
}

function drawSystems() {
    context.save();
    context.font = "600 13px Bahnschrift, Segoe UI, sans-serif";
    context.textBaseline = "middle";

    for (const system of scene.systems) {
        const pulse = Math.sin(scene.time * 3 + system.radius) * 2;
        context.strokeStyle = system.colour;
        context.globalAlpha = 0.35;
        context.lineWidth = 2;
        context.beginPath();
        context.arc(system.x, system.y, system.radius + 10 + pulse, 0, Math.PI * 2);
        context.stroke();

        context.globalAlpha = 1;
        context.fillStyle = system.colour;
        context.beginPath();
        context.arc(system.x, system.y, system.radius, 0, Math.PI * 2);
        context.fill();

        if (scene.width >= 760) {
            context.fillStyle = palette.text;
            context.fillText(system.name, system.x + system.radius + 12, system.y);
        }
    }

    context.restore();
}

function pseudo(seed) {
    const value = Math.sin(seed * 999) * 10000;
    return value - Math.floor(value);
}

window.addEventListener("resize", scheduleResize, { passive: true });
document.addEventListener("visibilitychange", syncAnimation);
reducedMotionQuery.addEventListener("change", syncAnimation);
resize();
syncAnimation();
