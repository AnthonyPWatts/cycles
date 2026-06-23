const state = {
    playerId: localStorage.getItem("cycles.playerId"),
    empire: null,
    galaxy: null,
    fleets: [],
    events: [],
    chronicle: []
};

const elements = {
    loginForm: document.querySelector("#loginForm"),
    username: document.querySelector("#username"),
    cycleStatus: document.querySelector("#cycleStatus"),
    empireName: document.querySelector("#empireName"),
    resources: document.querySelector("#resources"),
    fleets: document.querySelector("#fleets"),
    fleetSelect: document.querySelector("#fleetSelect"),
    destinationSelect: document.querySelector("#destinationSelect"),
    attackFleetSelect: document.querySelector("#attackFleetSelect"),
    targetEmpireSelect: document.querySelector("#targetEmpireSelect"),
    moveForm: document.querySelector("#moveForm"),
    attackForm: document.querySelector("#attackForm"),
    orderMessage: document.querySelector("#orderMessage"),
    events: document.querySelector("#events"),
    chronicle: document.querySelector("#chronicle"),
    galaxyMap: document.querySelector("#galaxyMap"),
    refreshButton: document.querySelector("#refreshButton")
};

elements.loginForm.addEventListener("submit", async event => {
    event.preventDefault();
    await login(elements.username.value);
});

elements.refreshButton.addEventListener("click", refresh);

elements.moveForm.addEventListener("submit", async event => {
    event.preventDefault();
    const fleetId = elements.fleetSelect.value;
    const targetSystemId = elements.destinationSelect.value;
    if (!fleetId || !targetSystemId) {
        setMessage("Select a fleet and linked destination.");
        return;
    }

    await postJson("/orders/fleet/move", { fleetId, targetSystemId });
    setMessage("Move order queued.");
    await refresh();
});

elements.attackForm.addEventListener("submit", async event => {
    event.preventDefault();
    const fleetId = elements.attackFleetSelect.value;
    const targetEmpireId = elements.targetEmpireSelect.value || null;
    if (!fleetId) {
        setMessage("Select an attacking fleet.");
        return;
    }

    await postJson("/orders/fleet/attack", { fleetId, targetEmpireId });
    setMessage("Attack order queued.");
    await refresh();
});

async function boot() {
    await login(elements.username.value);
}

async function login(username) {
    const login = await postJson("/auth/login", { username, empireName: null });
    state.playerId = login.playerId;
    state.empire = login.empire;
    localStorage.setItem("cycles.playerId", state.playerId);
    await refresh();
}

async function refresh() {
    const empireQuery = state.playerId ? `?playerId=${state.playerId}` : "";
    const [cycle, empire, galaxy, fleets, events, chronicle] = await Promise.all([
        getJson("/cycles/current"),
        getJson(`/empire${empireQuery}`),
        getJson("/galaxy"),
        state.empire ? getJson(`/fleets?empireId=${state.empire.empireId}`) : getJson("/fleets"),
        getJson("/events/recent?limit=20"),
        getJson("/chronicle")
    ]);

    state.empire = empire;
    state.galaxy = galaxy;
    state.fleets = fleets;
    state.events = events;
    state.chronicle = chronicle;

    renderCycle(cycle);
    renderEmpire(empire);
    renderFleets(fleets);
    renderOrders();
    renderEvents(events);
    renderChronicle(chronicle);
    renderGalaxy(galaxy, empire);
}

function renderCycle(cycle) {
    elements.cycleStatus.textContent = `${cycle.name} | tick ${cycle.currentTickNumber} | ${cycle.status}`;
}

function renderEmpire(empire) {
    elements.empireName.textContent = empire.empireName;
    const resources = empire.resources;
    elements.resources.innerHTML = `
        <dt>Industry</dt><dd>${formatNumber(resources.industry)}</dd>
        <dt>Research</dt><dd>${formatNumber(resources.research)}</dd>
        <dt>Population</dt><dd>${formatNumber(resources.population)}</dd>
        <dt>Home</dt><dd>${escapeHtml(empire.homeSystem.systemName)}</dd>
    `;
}

function renderFleets(fleets) {
    elements.fleets.innerHTML = fleets.map(item => {
        const fleet = item.fleet;
        const destination = item.destinationSystemName ? ` -> ${item.destinationSystemName}` : "";
        return `
            <article class="item">
                <strong>${escapeHtml(fleet.fleetName)}</strong>
                <span>${fleet.shipCount} ships | ${fleet.status} | ${escapeHtml(item.currentSystemName)}${escapeHtml(destination)}</span>
            </article>
        `;
    }).join("");
}

function renderOrders() {
    const activeFleets = state.fleets.filter(item => item.fleet.status === "active" && item.fleet.shipCount > 0);
    fillSelect(elements.fleetSelect, activeFleets, item => item.fleet.fleetId, item => item.fleet.fleetName);
    fillSelect(elements.attackFleetSelect, activeFleets, item => item.fleet.fleetId, item => item.fleet.fleetName);

    const selectedFleet = activeFleets.find(item => item.fleet.fleetId === elements.fleetSelect.value) ?? activeFleets[0];
    const destinations = selectedFleet ? linkedSystems(selectedFleet.fleet.currentSystemId) : [];
    fillSelect(elements.destinationSelect, destinations, item => item.systemId, item => item.systemName);

    const targetEmpires = collectTargetEmpires(selectedFleet);
    fillSelect(elements.targetEmpireSelect, targetEmpires, item => item.empireId, item => item.empireName, true);

    elements.fleetSelect.onchange = renderOrders;
    elements.attackFleetSelect.onchange = renderOrders;
}

function collectTargetEmpires(selectedFleet) {
    if (!selectedFleet || !state.galaxy) {
        return [];
    }

    const systemId = selectedFleet.fleet.currentSystemId;
    const presence = state.galaxy.presence.find(item => item.systemId === systemId);
    if (!presence) {
        return [];
    }

    const empireIds = Object.keys(presence.effectivePresence)
        .filter(id => id !== state.empire.empireId);

    return empireIds.map(id => ({ empireId: id, empireName: id.slice(0, 8) }));
}

function renderEvents(events) {
    elements.events.innerHTML = events.slice().reverse().map(event => `
        <article class="item">
            <strong>T${event.tickNumber}</strong>
            <span>${escapeHtml(event.displayText)}</span>
        </article>
    `).join("");
}

function renderChronicle(entries) {
    elements.chronicle.innerHTML = entries.length === 0
        ? `<article class="item"><span>No entries yet.</span></article>`
        : entries.map(entry => `
            <article class="item">
                <strong>${escapeHtml(entry.title)} (${entry.importanceScore})</strong>
                <span>${escapeHtml(entry.factualSummary)}</span>
            </article>
        `).join("");
}

function renderGalaxy(galaxy, empire) {
    const systems = new Map(galaxy.systems.map(system => [system.systemId, system]));
    const homeId = empire.homeSystem.systemId;
    const lines = galaxy.links.map(link => {
        const a = systems.get(link.systemAId);
        const b = systems.get(link.systemBId);
        return `<line class="link" x1="${a.x}" y1="${a.y}" x2="${b.x}" y2="${b.y}"></line>`;
    }).join("");

    const nodes = galaxy.systems.map(system => {
        const presence = galaxy.presence.find(item => item.systemId === system.systemId)?.effectivePresence ?? {};
        const ownPresence = Number(presence[empire.empireId] ?? 0);
        const radius = 7 + Math.min(16, Math.sqrt(ownPresence));
        const classes = [
            "system",
            system.historicalSignificance > 0 ? "historic" : "",
            system.systemId === homeId ? "home" : ""
        ].filter(Boolean).join(" ");

        return `
            ${ownPresence > 0 ? `<circle class="presence" cx="${system.x}" cy="${system.y}" r="${radius + 5}"></circle>` : ""}
            <circle class="${classes}" cx="${system.x}" cy="${system.y}" r="${radius}"></circle>
            <text class="system-label" x="${system.x + radius + 6}" y="${system.y + 4}">${escapeHtml(system.systemName)}</text>
        `;
    }).join("");

    elements.galaxyMap.innerHTML = `${lines}${nodes}`;
}

function linkedSystems(systemId) {
    if (!state.galaxy) {
        return [];
    }

    const systems = new Map(state.galaxy.systems.map(system => [system.systemId, system]));
    return state.galaxy.links
        .filter(link => link.systemAId === systemId || link.systemBId === systemId)
        .map(link => systems.get(link.systemAId === systemId ? link.systemBId : link.systemAId))
        .filter(Boolean)
        .sort((a, b) => a.systemName.localeCompare(b.systemName));
}

function fillSelect(select, items, value, label, includeEmpty = false) {
    const previous = select.value;
    const options = includeEmpty ? [`<option value="">Nearest hostile</option>`] : [];
    options.push(...items.map(item => `<option value="${value(item)}">${escapeHtml(label(item))}</option>`));
    select.innerHTML = options.join("");
    if ([...select.options].some(option => option.value === previous)) {
        select.value = previous;
    }
}

async function getJson(url) {
    const response = await fetch(url);
    return readResponse(response);
}

async function postJson(url, body) {
    const response = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body)
    });
    return readResponse(response);
}

async function readResponse(response) {
    const payload = await response.json();
    if (!response.ok) {
        throw new Error(payload.message ?? "Request failed.");
    }

    return payload;
}

function setMessage(message) {
    elements.orderMessage.textContent = message;
}

function formatNumber(value) {
    return Number(value).toLocaleString(undefined, { maximumFractionDigits: 2 });
}

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

boot().catch(error => {
    setMessage(error.message);
    console.error(error);
});
