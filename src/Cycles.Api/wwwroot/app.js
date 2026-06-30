const state = {
    playerId: localStorage.getItem("cycles.playerId"),
    empire: null,
    galaxy: null,
    selectedSystemId: null,
    selectedFleetId: null,
    fleetDetail: null,
    fleets: [],
    orders: [],
    events: [],
    chronicle: []
};

const elements = {
    loginForm: document.querySelector("#loginForm"),
    username: document.querySelector("#username"),
    cycleStatus: document.querySelector("#cycleStatus"),
    empireName: document.querySelector("#empireName"),
    resources: document.querySelector("#resources"),
    systemDetails: document.querySelector("#systemDetails"),
    priorityForm: document.querySelector("#priorityForm"),
    industryWeight: document.querySelector("#industryWeight"),
    researchWeight: document.querySelector("#researchWeight"),
    militaryWeight: document.querySelector("#militaryWeight"),
    expansionWeight: document.querySelector("#expansionWeight"),
    priorityTotal: document.querySelector("#priorityTotal"),
    priorityBars: document.querySelector("#priorityBars"),
    priorityMessage: document.querySelector("#priorityMessage"),
    fleets: document.querySelector("#fleets"),
    fleetDetails: document.querySelector("#fleetDetails"),
    fleetSelect: document.querySelector("#fleetSelect"),
    destinationSelect: document.querySelector("#destinationSelect"),
    attackFleetSelect: document.querySelector("#attackFleetSelect"),
    targetEmpireSelect: document.querySelector("#targetEmpireSelect"),
    moveForm: document.querySelector("#moveForm"),
    attackForm: document.querySelector("#attackForm"),
    orderMessage: document.querySelector("#orderMessage"),
    orders: document.querySelector("#orders"),
    events: document.querySelector("#events"),
    chronicle: document.querySelector("#chronicle"),
    galaxyMap: document.querySelector("#galaxyMap"),
    mapStats: document.querySelector("#mapStats"),
    refreshButton: document.querySelector("#refreshButton")
};

elements.loginForm.addEventListener("submit", async event => {
    event.preventDefault();
    await login(elements.username.value);
});

elements.refreshButton.addEventListener("click", refresh);

elements.galaxyMap.addEventListener("click", event => {
    const node = event.target.closest(".system-node");
    if (!node) {
        return;
    }

    selectSystem(node.dataset.systemId);
});

elements.galaxyMap.addEventListener("keydown", event => {
    if (event.key !== "Enter" && event.key !== " ") {
        return;
    }

    const node = event.target.closest(".system-node");
    if (!node) {
        return;
    }

    event.preventDefault();
    selectSystem(node.dataset.systemId);
});

elements.fleets.addEventListener("click", event => {
    const item = event.target.closest("[data-fleet-id]");
    if (!item) {
        return;
    }

    selectFleet(item.dataset.fleetId);
});

elements.fleets.addEventListener("keydown", event => {
    if (event.key !== "Enter" && event.key !== " ") {
        return;
    }

    const item = event.target.closest("[data-fleet-id]");
    if (!item) {
        return;
    }

    event.preventDefault();
    selectFleet(item.dataset.fleetId);
});

elements.priorityForm.addEventListener("input", updatePriorityTotal);

elements.priorityForm.addEventListener("submit", async event => {
    event.preventDefault();
    if (!state.empire) {
        setPriorityMessage("Login before updating priorities.");
        return;
    }

    const payload = {
        empireId: state.empire.empireId,
        industryWeight: parseWeight(elements.industryWeight.value),
        researchWeight: parseWeight(elements.researchWeight.value),
        militaryWeight: parseWeight(elements.militaryWeight.value),
        expansionWeight: parseWeight(elements.expansionWeight.value)
    };

    if (Object.values(payload).slice(1).reduce((total, value) => total + value, 0) === 0) {
        setPriorityMessage("At least one priority must be greater than zero.");
        return;
    }

    try {
        await postJson("/orders/priorities", payload);
        setPriorityMessage("Priorities saved.");
        await refresh();
    } catch (error) {
        setPriorityMessage(error.message);
    }
});

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
    const ordersQuery = state.empire ? `?empireId=${state.empire.empireId}` : "";
    const [cycle, empire, galaxy, fleets, orders, events, chronicle] = await Promise.all([
        getJson("/cycles/current"),
        getJson(`/empire${empireQuery}`),
        getJson("/galaxy"),
        state.empire ? getJson(`/fleets?empireId=${state.empire.empireId}`) : getJson("/fleets"),
        getJson(`/orders${ordersQuery}`),
        getJson("/events/recent?limit=20"),
        getJson("/chronicle")
    ]);

    state.empire = empire;
    state.galaxy = galaxy;
    state.fleets = fleets;
    state.orders = orders;
    state.events = events;
    state.chronicle = chronicle;

    if (!state.selectedFleetId || !fleets.some(item => item.fleet.fleetId === state.selectedFleetId)) {
        state.selectedFleetId = fleets[0]?.fleet.fleetId ?? null;
    }

    state.fleetDetail = state.selectedFleetId
        ? await getJson(`/fleets/${state.selectedFleetId}`)
        : null;

    if (!state.selectedSystemId || !galaxy.systems.some(system => system.systemId === state.selectedSystemId)) {
        state.selectedSystemId = empire.homeSystem.systemId;
    }

    renderCycle(cycle);
    renderEmpire(empire);
    renderSystemDetails();
    renderPriorities(empire.priorities);
    renderFleets(fleets);
    renderFleetDetails();
    renderOrders();
    renderOrderQueue(orders);
    renderEvents(events);
    renderChronicle(chronicle);
    renderGalaxy(galaxy, empire);
}

function renderCycle(cycle) {
    elements.cycleStatus.innerHTML = `
        <span class="cycle-name">${escapeHtml(cycle.name)}</span>
        <span class="cycle-pill">T${cycle.currentTickNumber}</span>
        ${statusChip(cycle.status)}
    `;
}

function renderEmpire(empire) {
    elements.empireName.textContent = empire.empireName;
    const resources = empire.resources;
    const maxResource = Math.max(1, Number(resources.industry), Number(resources.research), Number(resources.population));
    elements.resources.innerHTML = `
        ${resourceCard("Industry", resources.industry, maxResource)}
        ${resourceCard("Research", resources.research, maxResource)}
        ${resourceCard("Population", resources.population, maxResource)}
        <div class="resource-card resource-home">
            <dt>Home</dt>
            <dd>${escapeHtml(empire.homeSystem.systemName)}</dd>
        </div>
    `;
}

function renderPriorities(priorities) {
    elements.industryWeight.value = priorities.industryWeight;
    elements.researchWeight.value = priorities.researchWeight;
    elements.militaryWeight.value = priorities.militaryWeight;
    elements.expansionWeight.value = priorities.expansionWeight;
    updatePriorityTotal();
}

function renderFleets(fleets) {
    elements.fleets.innerHTML = fleets.length === 0
        ? `<article class="item"><span>No fleets yet.</span></article>`
        : fleets.map(item => {
        const fleet = item.fleet;
        const destination = item.destinationSystemName ? ` -> ${item.destinationSystemName}` : "";
        const selectedClass = fleet.fleetId === state.selectedFleetId ? " selected" : "";
        return `
            <article class="item fleet-item${selectedClass}" data-fleet-id="${fleet.fleetId}" role="button" tabindex="0">
                <strong>${escapeHtml(fleet.fleetName)}</strong>
                <span class="item-meta">
                    ${statusChip(fleet.status)}
                    <span>${fleet.shipCount} ships</span>
                    <span>${escapeHtml(item.currentSystemName)}${escapeHtml(destination)}</span>
                </span>
            </article>
        `;
    }).join("");
}

function renderFleetDetails() {
    const detail = state.fleetDetail;
    if (!detail) {
        elements.fleetDetails.innerHTML = `<article class="item"><span>No fleet selected.</span></article>`;
        return;
    }

    const destinationRows = detail.destinationSystem
        ? `
            <dt>Destination</dt><dd>${escapeHtml(detail.destinationSystem.systemName)}</dd>
            <dt>Arrival</dt><dd>${detail.arrivalTickNumber === null ? "Unknown" : `T${detail.arrivalTickNumber}`}</dd>
        `
        : "";

    const linkedSystems = detail.linkedSystems.length === 0
        ? `<span>No adjacent systems.</span>`
        : detail.linkedSystems.map(system => `<span>${escapeHtml(system.systemName)} (${system.strategicValue})</span>`).join("");

    const nearbyFleets = detail.activeFleetsInSystem.length === 0
        ? `<span>No other active fleets at this system.</span>`
        : detail.activeFleetsInSystem.map(fleet => `
            <span>${escapeHtml(fleet.fleetName)} | ${escapeHtml(fleet.empireName)} | ${fleet.shipCount} ships</span>
        `).join("");

    const orders = detail.orders.length === 0
        ? `<span>No orders recorded for this fleet.</span>`
        : detail.orders.map(order => {
            const target = order.targetSystemName ?? order.targetEmpireName ?? "nearest hostile";
            const timing = order.processedTick === null ? `after T${order.executeAfterTick}` : `processed T${order.processedTick}`;
            return `<span>${escapeHtml(formatOrderType(order.orderType))} | ${escapeHtml(order.status)} | ${escapeHtml(target)} | ${escapeHtml(timing)}</span>`;
        }).join("");

    elements.fleetDetails.innerHTML = `
        <article class="item">
            <strong>${escapeHtml(detail.fleetName)}</strong>
            <span class="item-meta">
                ${statusChip(detail.status)}
                <span>${detail.shipCount} ships</span>
                <span>${escapeHtml(detail.empireName)}</span>
            </span>
        </article>
        <dl class="detail-list">
            <dt>Current</dt><dd>${escapeHtml(detail.currentSystem.systemName)}</dd>
            <dt>Strategic</dt><dd>${detail.currentSystem.strategicValue}</dd>
            <dt>History</dt><dd>${detail.currentSystem.historicalSignificance}</dd>
            ${destinationRows}
        </dl>
        <div class="detail-block">
            <strong>Adjacent Routes</strong>
            ${linkedSystems}
        </div>
        <div class="detail-block">
            <strong>Local Fleets</strong>
            ${nearbyFleets}
        </div>
        <div class="detail-block">
            <strong>Orders</strong>
            ${orders}
        </div>
    `;
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

function renderSystemDetails() {
    if (!state.galaxy || !state.selectedSystemId) {
        elements.systemDetails.innerHTML = `<article class="item"><span>No system selected.</span></article>`;
        return;
    }

    const system = state.galaxy.systems.find(item => item.systemId === state.selectedSystemId);
    if (!system) {
        elements.systemDetails.innerHTML = `<article class="item"><span>No system selected.</span></article>`;
        return;
    }

    const presence = state.galaxy.presence.find(item => item.systemId === system.systemId)?.effectivePresence ?? {};
    const presenceRows = Object.entries(presence)
        .sort((first, second) => Number(second[1]) - Number(first[1]))
        .map(([empireId, value]) => {
            const label = empireId === state.empire.empireId ? state.empire.empireName : empireId.slice(0, 8);
            return `<dt>${escapeHtml(label)}</dt><dd>${formatNumber(value)}</dd>`;
        }).join("");

    elements.systemDetails.innerHTML = `
        <article class="item system-card">
            <strong>${escapeHtml(system.systemName)}</strong>
            <span class="item-meta">
                <span>${system.x}, ${system.y}</span>
                <span>Strategic ${system.strategicValue}</span>
                <span>History ${system.historicalSignificance}</span>
            </span>
        </article>
        <dl class="detail-list">
            <dt>Industry</dt><dd>${formatNumber(system.industryOutput)}</dd>
            <dt>Research</dt><dd>${formatNumber(system.researchOutput)}</dd>
            <dt>Population</dt><dd>${formatNumber(system.populationOutput)}</dd>
            ${presenceRows || "<dt>Presence</dt><dd>None</dd>"}
        </dl>
    `;
}

function renderOrderQueue(orders) {
    elements.orders.innerHTML = orders.length === 0
        ? `<article class="item"><span>No fleet orders yet.</span></article>`
        : orders.map(order => {
            const target = order.targetSystemName ?? order.targetEmpireName ?? "nearest hostile";
            const processed = order.processedTick === null ? `executes after T${order.executeAfterTick}` : `processed T${order.processedTick}`;
            const rejection = order.rejectionReason ? ` | ${order.rejectionReason}` : "";
            return `
                <article class="item order-${statusClass(order.status)}">
                    <strong>${escapeHtml(formatOrderType(order.orderType))}: ${escapeHtml(order.fleetName)}</strong>
                    <span class="item-meta">
                        ${statusChip(order.status)}
                        <span>${escapeHtml(target)}</span>
                        <span>${escapeHtml(processed)}${escapeHtml(rejection)}</span>
                    </span>
                </article>
            `;
        }).join("");
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
    elements.events.innerHTML = events.length === 0
        ? `<article class="item"><span>No events yet.</span></article>`
        : events.slice().reverse().map(event => `
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
        const activePresence = Object.values(presence).map(Number).filter(value => value > 0);
        const isContested = activePresence.length > 1;
        const radius = 7 + Math.min(16, Math.sqrt(ownPresence));
        const classes = [
            "system",
            system.historicalSignificance > 0 ? "historic" : "",
            system.systemId === homeId ? "home" : "",
            isContested ? "contested" : "",
            system.systemId === state.selectedSystemId ? "selected" : ""
        ].filter(Boolean).join(" ");
        const label = `${system.systemName}: strategic ${system.strategicValue}, presence ${formatNumber(ownPresence)}`;

        return `
            <g class="system-node" data-system-id="${system.systemId}" role="button" tabindex="0" aria-label="${escapeHtml(label)}">
                <title>${escapeHtml(label)}</title>
                ${ownPresence > 0 ? `<circle class="presence" cx="${system.x}" cy="${system.y}" r="${radius + 5}"></circle>` : ""}
                ${isContested ? `<circle class="contested-ring" cx="${system.x}" cy="${system.y}" r="${radius + 10}"></circle>` : ""}
                <circle class="${classes}" cx="${system.x}" cy="${system.y}" r="${radius}"></circle>
                <text class="system-label" x="${system.x + radius + 6}" y="${system.y + 4}">${escapeHtml(system.systemName)}</text>
            </g>
        `;
    }).join("");

    elements.galaxyMap.innerHTML = `${lines}${nodes}`;
    renderMapStats(galaxy);
}

function selectSystem(systemId) {
    state.selectedSystemId = systemId;
    renderSystemDetails();
    renderGalaxy(state.galaxy, state.empire);
}

async function selectFleet(fleetId) {
    state.selectedFleetId = fleetId;
    try {
        state.fleetDetail = await getJson(`/fleets/${fleetId}`);
        renderFleets(state.fleets);
        renderFleetDetails();
    } catch (error) {
        setMessage(error.message);
    }
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

function formatOrderType(value) {
    return String(value)
        .replace("moveFleet", "Move")
        .replace("hold", "Hold")
        .replace("attack", "Attack");
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

function setPriorityMessage(message) {
    elements.priorityMessage.textContent = message;
}

function updatePriorityTotal() {
    const priorities = [
        ["Industry", parseWeight(elements.industryWeight.value)],
        ["Research", parseWeight(elements.researchWeight.value)],
        ["Military", parseWeight(elements.militaryWeight.value)],
        ["Expansion", parseWeight(elements.expansionWeight.value)]
    ];
    const total = priorities.reduce((sum, [, value]) => sum + value, 0);

    elements.priorityTotal.textContent = total.toLocaleString();
    elements.priorityBars.innerHTML = priorities.map(([label, value]) => {
        const share = total === 0 ? 0 : Math.round(value / total * 100);
        return `
            <div class="priority-bar">
                <span>${escapeHtml(label)}</span>
                <strong>${value}</strong>
                <i style="width: ${share}%"></i>
            </div>
        `;
    }).join("");
}

function parseWeight(value) {
    const parsed = Number.parseInt(value, 10);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function formatNumber(value) {
    return Number(value).toLocaleString(undefined, { maximumFractionDigits: 2 });
}

function resourceCard(label, value, maxResource) {
    const numeric = Number(value);
    const width = numeric <= 0 ? 0 : Math.max(4, Math.round(numeric / maxResource * 100));
    return `
        <div class="resource-card">
            <dt>${escapeHtml(label)}</dt>
            <dd>
                <strong>${formatNumber(numeric)}</strong>
                <span class="resource-meter"><i style="width: ${width}%"></i></span>
            </dd>
        </div>
    `;
}

function renderMapStats(galaxy) {
    const contested = galaxy.presence.filter(item =>
        Object.values(item.effectivePresence).map(Number).filter(value => value > 0).length > 1).length;
    const owned = galaxy.presence.filter(item => Number(item.effectivePresence[state.empire.empireId] ?? 0) > 0).length;
    elements.mapStats.innerHTML = `
        ${statChip("Systems", galaxy.systems.length)}
        ${statChip("Routes", galaxy.links.length)}
        ${statChip("Held", owned)}
        ${statChip("Contested", contested)}
    `;
}

function statChip(label, value) {
    return `
        <span class="stat-chip">
            <strong>${formatNumber(value)}</strong>
            <em>${escapeHtml(label)}</em>
        </span>
    `;
}

function statusChip(value) {
    return `<span class="status-chip status-${statusClass(value)}">${escapeHtml(formatStatus(value))}</span>`;
}

function statusClass(value) {
    return String(value).toLowerCase().replace(/[^a-z0-9-]/g, "");
}

function formatStatus(value) {
    const spaced = String(value).replace(/([a-z])([A-Z])/g, "$1 $2");
    return spaced.charAt(0).toUpperCase() + spaced.slice(1);
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
