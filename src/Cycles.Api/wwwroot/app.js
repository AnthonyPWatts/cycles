const state = {
    playerId: null,
    role: null,
    canAdvanceTurn: false,
    cycle: null,
    empire: null,
    galaxy: null,
    selectedSystemId: null,
    selectedFleetId: null,
    fleetDetail: null,
    fleets: [],
    orders: [],
    events: [],
    chronicle: [],
    activeView: "command",
    orderHistoryLimit: 20
};

const viewIds = ["command", "galaxy", "fleets", "chronicle"];
const viewShortcuts = new Map([
    ["1", "command"],
    ["2", "galaxy"],
    ["3", "fleets"],
    ["4", "chronicle"]
]);

const tutorial = {
    version: "v1",
    active: false,
    status: "available",
    storageKey: null,
    stepIndex: 0,
    initialTick: 0,
    briefing: null,
    completedActions: new Set(),
    target: null,
    targetDescribedBy: null,
    returnFocus: null
};

const tutorialSessionStore = new Map();

const elements = {
    loginForm: document.querySelector("#loginForm"),
    username: document.querySelector("#username"),
    loginButton: document.querySelector("#loginButton"),
    loginMessage: document.querySelector("#loginMessage"),
    sessionSummary: document.querySelector("#sessionSummary"),
    sessionUsername: document.querySelector("#sessionUsername"),
    signOutButton: document.querySelector("#signOutButton"),
    appShell: document.querySelector("#appShell"),
    viewNav: document.querySelector("#viewNav"),
    views: [...document.querySelectorAll("[data-view]")],
    viewLinks: [...document.querySelectorAll("[data-view-link]")],
    commandPulse: document.querySelector("#commandPulse"),
    commandViewBadge: document.querySelector("#commandViewBadge"),
    galaxyViewBadge: document.querySelector("#galaxyViewBadge"),
    fleetsViewBadge: document.querySelector("#fleetsViewBadge"),
    chronicleViewBadge: document.querySelector("#chronicleViewBadge"),
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
    fleets: document.querySelector("#fleetList"),
    fleetDetails: document.querySelector("#fleetDetails"),
    fleetSelect: document.querySelector("#fleetSelect"),
    destinationSelect: document.querySelector("#destinationSelect"),
    attackFleetSelect: document.querySelector("#attackFleetSelect"),
    targetEmpireSelect: document.querySelector("#targetEmpireSelect"),
    coloniseFleetSelect: document.querySelector("#coloniseFleetSelect"),
    moveForm: document.querySelector("#moveForm"),
    attackForm: document.querySelector("#attackForm"),
    coloniseForm: document.querySelector("#coloniseForm"),
    orderMessage: document.querySelector("#orderMessage"),
    orders: document.querySelector("#orders"),
    orderHistory: document.querySelector("#orderHistory"),
    orderHistoryCount: document.querySelector("#orderHistoryCount"),
    events: document.querySelector("#events"),
    chronicle: document.querySelector("#chronicleEntries"),
    galaxyMap: document.querySelector("#galaxyMap"),
    mapStats: document.querySelector("#mapStats"),
    advanceTurnButton: document.querySelector("#advanceTurnButton"),
    turnMessage: document.querySelector("#turnMessage"),
    refreshButton: document.querySelector("#refreshButton"),
    tutorialButton: document.querySelector("#tutorialButton"),
    tutorialPanel: document.querySelector("#tutorialPanel"),
    tutorialProgress: document.querySelector("#tutorialProgress"),
    tutorialTitle: document.querySelector("#tutorialTitle"),
    tutorialBody: document.querySelector("#tutorialBody"),
    tutorialRequirement: document.querySelector("#tutorialRequirement"),
    tutorialPauseButton: document.querySelector("#tutorialPauseButton"),
    tutorialSkipButton: document.querySelector("#tutorialSkipButton"),
    tutorialBackButton: document.querySelector("#tutorialBackButton"),
    tutorialNextButton: document.querySelector("#tutorialNextButton")
};

elements.loginForm.addEventListener("submit", async event => {
    event.preventDefault();
    await login(elements.username.value.trim());
});

elements.signOutButton.addEventListener("click", signOut);

elements.refreshButton.addEventListener("click", refresh);

elements.tutorialButton.addEventListener("click", startOrResumeTutorial);
elements.tutorialPauseButton.addEventListener("click", pauseTutorial);
elements.tutorialSkipButton.addEventListener("click", skipTutorial);
elements.tutorialBackButton.addEventListener("click", previousTutorialStep);
elements.tutorialNextButton.addEventListener("click", nextTutorialStep);

document.addEventListener("keydown", event => {
    const shortcutView = event.altKey && !event.ctrlKey && !event.metaKey
        ? viewShortcuts.get(event.key)
        : null;
    if (shortcutView && !elements.appShell.hidden) {
        event.preventDefault();
        activateView(shortcutView, { updateLocation: true, focusHeading: true });
        return;
    }

    if (event.key === "Escape" && tutorial.active) {
        event.preventDefault();
        pauseTutorial();
    }
});

window.addEventListener("hashchange", () => {
    const requestedView = viewFromHash();
    activateView(requestedView ?? "command", {
        updateLocation: requestedView === null,
        focusHeading: true
    });
});

elements.advanceTurnButton.addEventListener("click", async () => {
    if (tutorial.active
        && tutorial.briefing
        && state.cycle?.currentTickNumber === 0
        && !curatedObjectiveOrdersReady()) {
        setTurnMessage("Complete the three Day 1 commitments before advancing the turn.");
        syncTutorialDisplay();
        return;
    }

    elements.advanceTurnButton.disabled = true;
    try {
        const result = await postJson("/admin/tick", {});
        setTurnMessage(`Advanced to T${result.tickNumber}: ${formatCount(result.ordersProcessed, "order")}, ${formatCount(result.eventsCreated, "event")}, ${formatCount(result.battlesCreated, "battle")}, ${formatCount(result.chronicleEntriesCreated, "Chronicle entry", "Chronicle entries")}.`);
        await refresh();
    } catch (error) {
        setTurnMessage(error.message);
    } finally {
        elements.advanceTurnButton.disabled = false;
    }
});

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
        industryWeight: parseWeight(elements.industryWeight.value),
        researchWeight: parseWeight(elements.researchWeight.value),
        militaryWeight: parseWeight(elements.militaryWeight.value),
        expansionWeight: parseWeight(elements.expansionWeight.value)
    };

    if (Object.values(payload).reduce((total, value) => total + value, 0) !== 100) {
        setPriorityMessage("Priorities must total 100.");
        return;
    }

    try {
        await postJson("/orders/priorities", payload);
        setPriorityMessage("Priorities saved.");
        await refresh();
        completeTutorialAction("prioritiesSaved");
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

    try {
        await postJson("/orders/fleet/move", { fleetId, targetSystemId });
        setMessage("Move order queued.");
        await refresh();
    } catch (error) {
        setMessage(error.message);
    }
});

elements.attackForm.addEventListener("submit", async event => {
    event.preventDefault();
    const fleetId = elements.attackFleetSelect.value;
    const targetEmpireId = elements.targetEmpireSelect.value || null;
    if (!fleetId) {
        setMessage("Select an attacking fleet.");
        return;
    }

    try {
        await postJson("/orders/fleet/attack", { fleetId, targetEmpireId });
        setMessage("Attack order queued.");
        await refresh();
    } catch (error) {
        setMessage(error.message);
    }
});

elements.coloniseForm.addEventListener("submit", async event => {
    event.preventDefault();
    const fleetId = elements.coloniseFleetSelect.value;
    if (!fleetId) {
        setMessage("Select a fleet outside its home system.");
        return;
    }

    try {
        await postJson("/orders/fleet/colonise", { fleetId });
        setMessage("Colonisation order queued.");
        await refresh();
    } catch (error) {
        setMessage(error.message);
    }
});

elements.orders.addEventListener("click", async event => {
    const button = event.target.closest("[data-cancel-order-id]");
    if (!button) {
        return;
    }

    await cancelOrder(button.dataset.cancelOrderId);
});

elements.orderHistory.addEventListener("click", event => {
    const button = event.target.closest("[data-load-more-orders]");
    if (!button) {
        return;
    }

    state.orderHistoryLimit += 20;
    renderOrderQueue(state.orders);
});

async function boot() {
    elements.username.value = readStoredValue("cycles.username") || elements.username.value;
    try {
        const session = await getJson("/auth/session");
        applySession(session);
    } catch (error) {
        showLogin("Enter your player name to continue.");
        elements.username.focus();
        return;
    }

    try {
        await refresh();
    } catch (error) {
        setTurnMessage(error.message);
    }
}

async function login(username) {
    if (!username) {
        showLogin("Enter your player name to continue.");
        elements.username.focus();
        return;
    }

    elements.loginButton.disabled = true;
    elements.loginMessage.textContent = "Signing in...";

    try {
        const login = await postJson("/auth/login", { username, empireName: null });
        applySession(login);
        writeStoredValue("cycles.username", login.username);
        removeStoredValue("cycles.playerId");
        await refresh();
    } catch (error) {
        showLogin(error.message);
    } finally {
        elements.loginButton.disabled = false;
    }
}

async function signOut() {
    elements.signOutButton.disabled = true;

    try {
        await postJson("/auth/logout", {});
        showLogin("You have signed out. Enter your player name to continue.");
        elements.username.focus();
    } catch (error) {
        setTurnMessage(error.message);
    } finally {
        elements.signOutButton.disabled = false;
    }
}

function applySession(login) {
    const playerChanged = state.playerId !== login.playerId;
    if (state.playerId && playerChanged) {
        resetTutorialContext();
    }
    if (playerChanged) {
        state.orderHistoryLimit = 20;
    }

    state.playerId = login.playerId;
    state.role = login.role;
    state.canAdvanceTurn = login.canAdvanceTurn;
    state.empire = login.empire;
    elements.advanceTurnButton.hidden = !login.canAdvanceTurn;
    elements.sessionUsername.textContent = login.username;
    elements.loginForm.hidden = true;
    elements.sessionSummary.hidden = false;
    elements.appShell.hidden = false;
    activateView(resolveInitialView(), { updateLocation: true });
}

function showLogin(message) {
    if (state.playerId) {
        resetTutorialContext();
    }

    state.playerId = null;
    state.role = null;
    state.canAdvanceTurn = false;
    state.empire = null;
    elements.loginMessage.textContent = message;
    elements.loginForm.hidden = false;
    elements.sessionSummary.hidden = true;
    elements.appShell.hidden = true;
}

async function refresh() {
    const [cycle, empire, galaxy, fleets, orders, events, chronicle] = await Promise.all([
        getJson("/cycles/current"),
        getJson("/empire"),
        getJson("/galaxy"),
        getJson("/fleets"),
        getJson("/orders"),
        getJson("/events/recent?limit=20"),
        getJson("/chronicle")
    ]);

    state.empire = empire;
    state.cycle = cycle;
    state.galaxy = galaxy;
    state.fleets = fleets;
    state.orders = orders;
    state.events = events;
    state.chronicle = chronicle;

    if (!state.selectedFleetId || !fleets.some(item => item.fleet.fleetId === state.selectedFleetId)) {
        const defaultFleet = fleets.find(item => item.fleet.status === "active" && item.fleet.shipCount > 0) ?? fleets[0];
        state.selectedFleetId = defaultFleet?.fleet.fleetId ?? null;
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
    renderCommandSummary();
    syncTutorialAfterRefresh();
}

function viewFromHash() {
    const value = window.location.hash.slice(1).toLowerCase();
    return viewIds.includes(value) ? value : null;
}

function resolveInitialView() {
    const requestedView = viewFromHash();
    if (requestedView) {
        return requestedView;
    }

    const storedView = readStoredValue("cycles.activeView");
    return viewIds.includes(storedView) ? storedView : "command";
}

function activateView(viewId, { updateLocation = false, focusHeading = false } = {}) {
    const selectedView = viewIds.includes(viewId) ? viewId : "command";
    state.activeView = selectedView;

    for (const view of elements.views) {
        view.hidden = view.dataset.view !== selectedView;
    }

    for (const link of elements.viewLinks) {
        if (link.dataset.viewLink === selectedView) {
            link.setAttribute("aria-current", "page");
        } else {
            link.removeAttribute("aria-current");
        }
    }

    writeStoredValue("cycles.activeView", selectedView);
    if (updateLocation && window.location.hash !== `#${selectedView}`) {
        window.history.replaceState(null, "", `#${selectedView}`);
    }

    if (focusHeading) {
        const heading = document.querySelector(`[data-view="${selectedView}"] h1`);
        requestAnimationFrame(() => heading?.focus({ preventScroll: true }));
    }
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
        ${resourceCard("Industry", resources.industry, maxResource, resources.lastGeneratedIndustry, resources.lastSpentIndustry)}
        ${resourceCard("Research", resources.research, maxResource, resources.lastGeneratedResearch, resources.lastSpentResearch)}
        ${resourceCard("Population", resources.population, maxResource, resources.lastGeneratedPopulation, resources.lastSpentPopulation)}
        <div class="resource-card resource-home">
            <dt>Home</dt>
            <dd>${escapeHtml(empire.homeSystem.systemName)}</dd>
        </div>
    `;
}

function renderCommandSummary() {
    const pendingOrders = state.orders.filter(order => order.status === "pending").length;
    const activeFleets = state.fleets.filter(item => item.fleet.status === "active" && item.fleet.shipCount > 0).length;
    const visibleEvents = state.events.length;
    const chronicleEntries = state.chronicle.length;

    elements.commandPulse.innerHTML = `
        ${commandPulseLink("Pending orders", pendingOrders, "fleets", pendingOrders === 0 ? "Issue an order" : "Review commitments")}
        ${commandPulseLink("Active fleets", activeFleets, "fleets", "Open fleet command")}
        ${commandPulseLink("Recent events", visibleEvents, "chronicle", "Read the audit trail")}
        ${commandPulseLink("Chronicle entries", chronicleEntries, "chronicle", "Read recorded history")}
    `;

    setViewBadge(elements.commandViewBadge, pendingOrders, `${formatCount(pendingOrders, "pending order")}`);
    setViewBadge(elements.galaxyViewBadge, state.galaxy?.systems.length ?? 0, `${formatCount(state.galaxy?.systems.length ?? 0, "system")}`);
    setViewBadge(elements.fleetsViewBadge, activeFleets, `${formatCount(activeFleets, "active fleet")}`);
    setViewBadge(elements.chronicleViewBadge, chronicleEntries, `${formatCount(chronicleEntries, "Chronicle entry", "Chronicle entries")}`);
}

function commandPulseLink(label, value, viewId, action) {
    return `
        <a class="pulse-card" href="#${viewId}">
            <span>${escapeHtml(label)}</span>
            <strong>${formatNumber(value)}</strong>
            <small>${escapeHtml(action)}</small>
        </a>
    `;
}

function setViewBadge(element, value, label) {
    element.textContent = formatNumber(value);
    element.setAttribute("aria-label", label);
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
        : fleets.slice().sort((left, right) => {
            const leftActive = left.fleet.status === "active" && left.fleet.shipCount > 0;
            const rightActive = right.fleet.status === "active" && right.fleet.shipCount > 0;
            return Number(rightActive) - Number(leftActive)
                || left.fleet.fleetName.localeCompare(right.fleet.fleetName);
        }).map(item => {
        const fleet = item.fleet;
        const destination = item.destinationSystemName ? ` -> ${item.destinationSystemName}` : "";
        const selectedClass = fleet.fleetId === state.selectedFleetId ? " selected" : "";
        const admiral = item.admiral ? `<span>${escapeHtml(formatAdmiral(item.admiral))}</span>` : "";
        return `
            <article class="item fleet-item${selectedClass}" data-fleet-id="${fleet.fleetId}" role="button" tabindex="0">
                <strong>${escapeHtml(fleet.fleetName)}</strong>
                <span class="item-meta">
                    ${statusChip(fleet.status)}
                    <span>${fleet.shipCount} ships</span>
                    <span>${escapeHtml(item.currentSystemName)}${escapeHtml(destination)}</span>
                    ${admiral}
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
        : detail.activeFleetsInSystem.map(fleet => {
            const admiral = fleet.admiral ? ` | ${formatAdmiral(fleet.admiral)}` : "";
            return `
            <span>${escapeHtml(fleet.fleetName)} | ${escapeHtml(fleet.empireName)} | ${fleet.shipCount} ships${escapeHtml(admiral)}</span>
        `;
        }).join("");

    const admiralRows = detail.admiral
        ? `
            <dt>Admiral</dt><dd>${escapeHtml(detail.admiral.admiralName)}</dd>
            <dt>Reputation</dt><dd>${formatNumber(detail.admiral.reputationScore)} | ${escapeHtml(formatStatus(detail.admiral.status))}</dd>
        `
        : `
            <dt>Admiral</dt><dd>Unassigned</dd>
        `;

    const orders = detail.orders.length === 0
        ? `<span>No orders recorded for this fleet.</span>`
        : detail.orders.map(order => {
            const target = order.targetSystemName ?? order.targetEmpireName ?? "nearest hostile";
            const timing = formatOrderTiming(order);
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
            ${admiralRows}
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
    fillSelect(elements.fleetSelect, activeFleets, item => item.fleet.fleetId, fleetSelectLabel);
    fillSelect(elements.attackFleetSelect, activeFleets, item => item.fleet.fleetId, fleetSelectLabel);
    const colonisingFleets = activeFleets.filter(item => item.fleet.currentSystemId !== state.empire.homeSystem.systemId);
    fillSelect(elements.coloniseFleetSelect, colonisingFleets, item => item.fleet.fleetId, fleetSelectLabel);

    const selectedFleet = activeFleets.find(item => item.fleet.fleetId === elements.fleetSelect.value) ?? activeFleets[0];
    const destinations = selectedFleet ? linkedSystems(selectedFleet.fleet.currentSystemId) : [];
    fillSelect(elements.destinationSelect, destinations, item => item.systemId, item => item.systemName);

    const selectedAttackFleet = activeFleets.find(item => item.fleet.fleetId === elements.attackFleetSelect.value) ?? activeFleets[0];
    const targetEmpires = collectTargetEmpires(selectedAttackFleet);
    fillSelect(elements.targetEmpireSelect, targetEmpires, item => item.empireId, item => item.empireName, true);

    elements.fleetSelect.onchange = renderOrders;
    elements.attackFleetSelect.onchange = async () => {
        await selectFleet(elements.attackFleetSelect.value);
        renderOrders();
    };
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
    const outposts = state.galaxy.colonialOutposts
        .filter(item => item.systemId === system.systemId)
        .map(item => `<span>${escapeHtml(item.empireName)} | established T${item.establishedTick} | ${item.isProjectingPresence ? "projecting" : "inactive"}</span>`)
        .join("");

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
        <div class="detail-block">
            <strong>Colonial Outposts</strong>
            ${outposts || "<span>None established.</span>"}
        </div>
    `;
}

function renderOrderQueue(orders) {
    const pendingOrders = orders.filter(order => order.status === "pending");
    elements.orders.innerHTML = pendingOrders.length === 0
        ? `<article class="item empty-state"><strong>No pending orders</strong><span>Issue an order when you are ready to commit the next turn.</span></article>`
        : pendingOrders.map(order => orderCard(order, true)).join("");

    const resolvedOrders = orders
        .filter(order => order.status !== "pending")
        .slice()
        .reverse();
    const visibleOrders = resolvedOrders.slice(0, state.orderHistoryLimit);
    const remaining = resolvedOrders.length - visibleOrders.length;
    const loadMore = remaining > 0
        ? `<button type="button" class="history-load-more" data-load-more-orders>Show ${formatCount(Math.min(20, remaining), "more order")}</button>`
        : "";

    elements.orderHistoryCount.textContent = resolvedOrders.length === 0
        ? "No resolved orders"
        : `Showing ${formatNumber(visibleOrders.length)} of ${formatNumber(resolvedOrders.length)}`;
    elements.orderHistory.innerHTML = resolvedOrders.length === 0
        ? `<article class="item empty-state"><span>Resolved and cancelled orders will appear here.</span></article>`
        : `${visibleOrders.map(order => orderCard(order, false)).join("")}${loadMore}`;
}

function orderCard(order, allowCancel) {
    const target = order.targetSystemName ?? order.targetEmpireName ?? "nearest hostile";
    const timing = formatOrderTiming(order);
    const rejection = order.rejectionReason ? ` | ${order.rejectionReason}` : "";
    const cancelButton = allowCancel
        ? `<button type="button" class="inline-action" data-cancel-order-id="${order.fleetOrderId}">Cancel</button>`
        : "";
    return `
        <article class="item order-${statusClass(order.status)}">
            <strong>${escapeHtml(formatOrderType(order.orderType))}: ${escapeHtml(order.fleetName)}</strong>
            <span class="item-meta">
                ${statusChip(order.status)}
                <span>${escapeHtml(target)}</span>
                <span>${escapeHtml(timing)}${escapeHtml(rejection)}</span>
            </span>
            ${cancelButton}
        </article>
    `;
}

async function cancelOrder(fleetOrderId) {
    if (!state.empire) {
        setMessage("Login before cancelling orders.");
        return;
    }

    try {
        await postJson("/orders/fleet/cancel", { fleetOrderId });
        setMessage("Order cancelled.");
        await refresh();
    } catch (error) {
        setMessage(error.message);
    }
}

function syncTutorialAfterRefresh() {
    const storageKey = state.playerId && state.cycle
        ? `cycles.tutorial.${tutorial.version}.${state.playerId}.${state.cycle.cycleId}.${state.cycle.createdAt}`
        : null;

    if (!storageKey) {
        return;
    }

    if (tutorial.storageKey !== storageKey) {
        clearTutorialTarget();
        tutorial.storageKey = storageKey;
        tutorial.briefing = findOpeningBriefing();
        tutorial.completedActions = new Set();
        tutorial.initialTick = state.cycle.currentTickNumber;
        tutorial.stepIndex = 0;
        tutorial.status = "available";
        tutorial.active = false;

        const saved = loadTutorialState(storageKey);
        if (saved) {
            tutorial.status = saved.status ?? "available";
            tutorial.initialTick = Number.isInteger(saved.initialTick) ? saved.initialTick : state.cycle.currentTickNumber;
            tutorial.completedActions = new Set(Array.isArray(saved.completedActions) ? saved.completedActions : []);
            tutorial.briefing = saved.briefing ?? tutorial.briefing;
            const savedIndex = tutorialSteps().findIndex(step => step.id === saved.stepId);
            tutorial.stepIndex = savedIndex >= 0 ? savedIndex : 0;
            tutorial.active = tutorial.status === "active";
        } else if (tutorial.briefing && state.cycle.currentTickNumber === 0 && state.orders.length === 0) {
            tutorial.status = "active";
            tutorial.active = true;
            tutorial.initialTick = state.cycle.currentTickNumber;
            saveTutorialState();
        }
    }

    syncTutorialDisplay();
}

function startOrResumeTutorial() {
    if (!tutorial.storageKey || !state.cycle) {
        return;
    }

    tutorial.returnFocus = elements.tutorialButton;
    if (tutorial.status === "paused") {
        tutorial.status = "active";
        tutorial.active = true;
    } else if (!tutorial.active) {
        tutorial.status = "active";
        tutorial.active = true;
        tutorial.stepIndex = 0;
        tutorial.initialTick = state.cycle.currentTickNumber;
        tutorial.completedActions = new Set();
        tutorial.briefing = findOpeningBriefing() ?? tutorial.briefing;
    }

    saveTutorialState();
    renderTutorial({ focusHeading: true });
}

function pauseTutorial() {
    if (!tutorial.active) {
        return;
    }

    tutorial.status = "paused";
    tutorial.active = false;
    saveTutorialState();
    hideTutorial();
}

function skipTutorial() {
    tutorial.status = "skipped";
    tutorial.active = false;
    saveTutorialState();
    hideTutorial();
}

function completeTutorial() {
    tutorial.status = "completed";
    tutorial.active = false;
    saveTutorialState();
    hideTutorial();
}

function previousTutorialStep() {
    if (!tutorial.active || tutorial.stepIndex === 0) {
        return;
    }

    tutorial.stepIndex--;
    saveTutorialState();
    renderTutorial({ focusHeading: true });
}

function nextTutorialStep() {
    if (!tutorial.active) {
        return;
    }

    const steps = tutorialSteps();
    const step = steps[tutorial.stepIndex];
    if (step.required && !step.isSatisfied()) {
        return;
    }

    if (tutorial.stepIndex >= steps.length - 1) {
        completeTutorial();
        return;
    }

    tutorial.stepIndex++;
    saveTutorialState();
    renderTutorial({ focusHeading: true });
}

function completeTutorialAction(action) {
    if (!tutorial.storageKey) {
        return;
    }

    tutorial.completedActions.add(action);
    saveTutorialState();
    syncTutorialDisplay();
}

function syncTutorialDisplay() {
    updateTutorialButton();
    if (tutorial.active) {
        renderTutorial({ focusHeading: elements.tutorialPanel.hidden });
    } else {
        elements.tutorialPanel.hidden = true;
        document.body.classList.remove("tutorial-active");
        clearTutorialTarget();
    }
}

function renderTutorial({ focusHeading }) {
    const steps = tutorialSteps();
    tutorial.stepIndex = Math.min(tutorial.stepIndex, steps.length - 1);
    const step = steps[tutorial.stepIndex];
    const satisfied = !step.required || step.isSatisfied();

    if (step.view) {
        activateView(step.view, { updateLocation: true });
    }

    clearTutorialTarget();
    elements.tutorialPanel.hidden = false;
    document.body.classList.add("tutorial-active");
    elements.tutorialProgress.textContent = `${tutorial.stepIndex + 1} of ${steps.length}`;
    elements.tutorialTitle.textContent = step.title;
    elements.tutorialBody.textContent = step.body;
    elements.tutorialRequirement.textContent = step.required
        ? satisfied ? "Done. Continue when you are ready." : step.requirement
        : "";
    elements.tutorialBackButton.disabled = tutorial.stepIndex === 0;
    elements.tutorialNextButton.disabled = !satisfied;
    elements.tutorialNextButton.textContent = tutorial.stepIndex === 0
        ? "Start"
        : tutorial.stepIndex === steps.length - 1 ? "Finish" : satisfied ? "Next" : "Complete this step";

    const target = step.target?.();
    if (target) {
        applyTutorialTarget(target);
    }

    if (focusHeading) {
        requestAnimationFrame(() => elements.tutorialTitle.focus({ preventScroll: true }));
    }
}

function hideTutorial() {
    elements.tutorialPanel.hidden = true;
    document.body.classList.remove("tutorial-active");
    clearTutorialTarget();
    updateTutorialButton();
    (tutorial.returnFocus ?? elements.tutorialButton).focus();
    tutorial.returnFocus = null;
}

function updateTutorialButton() {
    elements.tutorialButton.textContent = tutorial.active
        ? "Guide open"
        : tutorial.status === "paused" ? "Resume guide"
            : tutorial.status === "completed" || tutorial.status === "skipped" ? "Restart guide"
                : "Guide";
    elements.tutorialButton.setAttribute("aria-expanded", String(tutorial.active));
}

function applyTutorialTarget(target) {
    tutorial.target = target;
    tutorial.targetDescribedBy = target.getAttribute("aria-describedby");
    target.classList.add("tutorial-target");
    const describedBy = new Set((tutorial.targetDescribedBy ?? "").split(/\s+/).filter(Boolean));
    describedBy.add("tutorialBody");
    target.setAttribute("aria-describedby", [...describedBy].join(" "));

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    requestAnimationFrame(() => target.scrollIntoView({
        behavior: reducedMotion ? "auto" : "smooth",
        block: "center",
        inline: "nearest"
    }));
}

function clearTutorialTarget() {
    if (!tutorial.target) {
        return;
    }

    tutorial.target.classList.remove("tutorial-target");
    if (tutorial.targetDescribedBy) {
        tutorial.target.setAttribute("aria-describedby", tutorial.targetDescribedBy);
    } else {
        tutorial.target.removeAttribute("aria-describedby");
    }

    tutorial.target = null;
    tutorial.targetDescribedBy = null;
}

function resetTutorialContext() {
    clearTutorialTarget();
    elements.tutorialPanel.hidden = true;
    document.body.classList.remove("tutorial-active");
    tutorial.active = false;
    tutorial.status = "available";
    tutorial.storageKey = null;
    tutorial.stepIndex = 0;
    tutorial.initialTick = 0;
    tutorial.briefing = null;
    tutorial.completedActions = new Set();
}

function findOpeningBriefing() {
    for (const event of state.events) {
        if (event.eventType !== "openingBriefingIssued" || event.empireId !== state.empire?.empireId) {
            continue;
        }

        try {
            const facts = JSON.parse(event.factJson);
            if (facts.scenarioKey === "development-cold-start-v1") {
                return {
                    scenarioKey: facts.scenarioKey,
                    focusSystemId: facts.focusSystemId,
                    objectives: facts.objectives,
                    displayText: event.displayText
                };
            }
        } catch {
            // A malformed optional briefing should not block the dashboard.
        }
    }

    return null;
}

function tutorialSteps() {
    const briefing = tutorial.briefing;
    const move = briefing?.objectives?.move;
    const colonise = briefing?.objectives?.colonise;
    const attack = briefing?.objectives?.attack;
    const focusSystemId = briefing?.focusSystemId ?? state.empire?.homeSystem?.systemId;
    const focusFleetId = attack?.fleetId ?? state.fleets[0]?.fleet?.fleetId;
    const moveFleetId = move?.fleetId ?? state.fleets[0]?.fleet?.fleetId;
    const moveTargetId = move?.targetSystemId ?? linkedSystems(state.fleets[0]?.fleet?.currentSystemId)[0]?.systemId;
    const curated = Boolean(move && colonise && attack);
    const steps = [
        {
            id: "welcome",
            view: "command",
            title: curated ? "Three fronts need orders" : "Command your first turn",
            body: curated
                ? "Treaty Gate is contested, Pale Harbour can support an outpost, and Nadir Crossing is open. This guide will help you issue all three orders, advance the turn, and read the consequences."
                : "Your command loop is simple: inspect the galaxy, set priorities, issue an order, advance the turn, and review what changed. This guide uses the real controls and real simulation.",
            required: false
        },
        {
            id: "resources",
            view: "command",
            title: "Know what you can spend",
            body: "Industry funds ships through Military priority. Research accumulates towards Survey Projection. Population pays for outposts. The small figures show what the previous turn generated and spent.",
            target: () => document.querySelector("#resourcesSection"),
            required: false
        },
        {
            id: "priorities",
            view: "command",
            title: "Choose what this turn emphasises",
            body: "Priorities must total 100. Military converts industry into ship construction; Expansion strengthens your projected influence. Adjust them or keep the opening allocation, then save.",
            target: () => document.querySelector("#prioritySection"),
            required: true,
            requirement: "Save a valid priority allocation to continue.",
            isSatisfied: () => tutorial.completedActions.has("prioritiesSaved")
        },
        {
            id: "map",
            view: "galaxy",
            title: curated ? "Find the flashpoint" : "Read the galaxy",
            body: curated
                ? `${tutorialSystemName(focusSystemId)} has a red contested ring because both sides have active fleets there. Select it to inspect the local position.`
                : "Routes define where fleets can move. Rings show your presence, and red marks a contested system. Select your home system to inspect it.",
            target: () => document.querySelector(`.system-node[data-system-id="${focusSystemId}"]`),
            required: true,
            requirement: "Select the highlighted system on the map.",
            isSatisfied: () => state.selectedSystemId === focusSystemId
        },
        {
            id: "fleet",
            view: "fleets",
            title: curated ? "Inspect the Vanguard" : "Inspect your fleet",
            body: curated
                ? `Select ${tutorialFleetName(focusFleetId)}. Fleet detail shows its ships, commander, current system, local rivals, and recorded orders.`
                : "Select your fleet. Fleet detail shows its ships, commander, current system, adjacent routes, and recorded orders.",
            target: () => document.querySelector(`[data-fleet-id="${focusFleetId}"]`),
            required: true,
            requirement: "Select the highlighted fleet.",
            isSatisfied: () => state.selectedFleetId === focusFleetId
        },
        {
            id: "move",
            view: "fleets",
            title: curated ? "Secure Nadir Crossing" : "Commit a movement order",
            body: curated
                ? `In Move, choose ${tutorialFleetName(moveFleetId)} and ${tutorialSystemName(moveTargetId)}. Orders are intentions: the server validates them again when the turn resolves.`
                : `Choose ${tutorialFleetName(moveFleetId)} and ${tutorialSystemName(moveTargetId)}. The order will resolve on the next authoritative turn.`,
            target: () => document.querySelector("#moveForm"),
            required: true,
            requirement: "Queue the highlighted movement objective.",
            isSatisfied: () => tutorialOrderExists("moveFleet", moveFleetId, "targetSystemId", moveTargetId)
        }
    ];

    if (curated) {
        steps.push(
            {
                id: "colonise",
                view: "fleets",
                title: "Establish the Pale Harbour outpost",
                body: `Choose ${tutorialFleetName(colonise.fleetId)}. Colonisation costs 100 population and succeeds because that fleet has the leading local influence.`,
                target: () => document.querySelector("#coloniseForm"),
                required: true,
                requirement: "Queue the Pale Harbour outpost.",
                isSatisfied: () => tutorialOrderExists("colonise", colonise.fleetId)
            },
            {
                id: "attack",
                view: "fleets",
                title: "Answer the Khepri challenge",
                body: `Choose ${tutorialFleetName(attack.fleetId)} and the local Khepri force. Combat is deterministic from persisted facts, but victory is not scripted. Treaty Gate is important enough that the result will enter the Chronicle.`,
                target: () => document.querySelector("#attackForm"),
                required: true,
                requirement: "Queue the Treaty Gate attack.",
                isSatisfied: () => tutorialOrderExists("attack", attack.fleetId, "targetEmpireId", attack.targetEmpireId)
            }
        );
    }

    steps.push(
        {
            id: "queue",
            view: "command",
            title: "Review your commitments",
            body: curated
                ? "The queue should now hold three pending orders. You can cancel any pending intention before the turn resolves."
                : "The queue records when the order will execute. You can cancel a pending intention before the turn resolves.",
            target: () => document.querySelector("#orderQueueSection"),
            required: curated,
            requirement: curated ? "Keep exactly the three highlighted commitments ready for Day 1." : "",
            isSatisfied: () => !curated || curatedObjectiveOrdersReady()
        },
        {
            id: "advance",
            title: "Resolve the turn",
            body: "Advance turn runs the same authoritative simulation boundary as the Worker and CLI. It resolves the whole development galaxy, not only your empire.",
            target: () => elements.advanceTurnButton,
            required: true,
            requirement: "Advance the development galaxy by one turn.",
            isSatisfied: () => state.cycle?.currentTickNumber > tutorial.initialTick
        },
        {
            id: "events",
            view: "chronicle",
            title: "Read what actually happened",
            body: "Events are the factual audit trail. Check which orders processed, what resources changed, and whether anything was rejected when the world moved underneath an intention.",
            target: () => document.querySelector("#eventsSection"),
            required: false
        }
    );

    if (curated) {
        steps.push({
            id: "chronicle",
            view: "chronicle",
            title: "See what became history",
            body: "The Chronicle preserves exceptional events, not every routine action. Treaty Gate appears here because its battle crossed the importance threshold using real losses, strategy, and prior history.",
            target: () => document.querySelector("#chronicleSection"),
            required: false
        });
    }

    steps.push({
        id: "next",
        view: "command",
        title: "That is the Cycles loop",
        body: "Inspect, prioritise, commit orders, resolve the turn, then read the consequences. From here, reinforce pressure, build ships, found outposts, or seek another battle worth remembering.",
        required: false
    });

    return steps;
}

function tutorialOrderExists(orderType, fleetId, targetProperty, targetId) {
    return state.orders.some(order =>
        order.orderType === orderType
        && order.fleetId === fleetId
        && order.status !== "cancelled"
        && order.status !== "rejected"
        && (!targetProperty || order[targetProperty] === targetId));
}

function curatedObjectiveOrdersReady() {
    const objectives = tutorial.briefing?.objectives;
    if (!objectives?.move || !objectives.colonise || !objectives.attack) {
        return false;
    }

    const expectedOrders = [
        {
            orderType: "moveFleet",
            fleetId: objectives.move.fleetId,
            targetProperty: "targetSystemId",
            targetId: objectives.move.targetSystemId
        },
        {
            orderType: "colonise",
            fleetId: objectives.colonise.fleetId
        },
        {
            orderType: "attack",
            fleetId: objectives.attack.fleetId,
            targetProperty: "targetEmpireId",
            targetId: objectives.attack.targetEmpireId
        }
    ];

    if (state.cycle?.currentTickNumber !== 0) {
        return expectedOrders.every(expected => tutorialOrderExists(
            expected.orderType,
            expected.fleetId,
            expected.targetProperty,
            expected.targetId));
    }

    const objectiveFleetIds = new Set(expectedOrders.map(expected => expected.fleetId));
    const pendingObjectiveOrders = state.orders.filter(order =>
        order.status === "pending" && objectiveFleetIds.has(order.fleetId));

    return pendingObjectiveOrders.length === expectedOrders.length
        && expectedOrders.every(expected => pendingObjectiveOrders.some(order =>
            order.orderType === expected.orderType
            && order.fleetId === expected.fleetId
            && (!expected.targetProperty || order[expected.targetProperty] === expected.targetId)));
}

function tutorialFleetName(fleetId) {
    return state.fleets.find(item => item.fleet.fleetId === fleetId)?.fleet.fleetName ?? "the highlighted fleet";
}

function tutorialSystemName(systemId) {
    return state.galaxy?.systems.find(system => system.systemId === systemId)?.systemName ?? "the highlighted system";
}

function saveTutorialState() {
    if (!tutorial.storageKey) {
        return;
    }

    const steps = tutorialSteps();
    const value = {
        status: tutorial.status,
        stepId: steps[tutorial.stepIndex]?.id ?? "welcome",
        initialTick: tutorial.initialTick,
        completedActions: [...tutorial.completedActions],
        briefing: tutorial.briefing
    };
    tutorialSessionStore.set(tutorial.storageKey, value);
    writeStoredValue(tutorial.storageKey, JSON.stringify(value));
}

function loadTutorialState(storageKey) {
    const value = readStoredValue(storageKey);
    if (value) {
        try {
            return JSON.parse(value);
        } catch {
            removeStoredValue(storageKey);
        }
    }

    return tutorialSessionStore.get(storageKey) ?? null;
}

function readStoredValue(key) {
    try {
        return localStorage.getItem(key);
    } catch {
        return null;
    }
}

function writeStoredValue(key, value) {
    try {
        localStorage.setItem(key, value);
    } catch {
        // Storage-restricted browsers keep tutorial state in memory for this session.
    }
}

function removeStoredValue(key) {
    try {
        localStorage.removeItem(key);
    } catch {
        // There is no persistent value to remove when storage is unavailable.
    }
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

    const visibleEmpireNames = new Map(
        (state.fleetDetail?.activeFleetsInSystem ?? []).map(fleet => [fleet.empireId, fleet.empireName]));

    return empireIds.map(id => ({
        empireId: id,
        empireName: visibleEmpireNames.get(id) ?? id.slice(0, 8)
    }));
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
        : entries.map(entry => {
            const narrative = entry.narrativeText || entry.factualSummary;
            const factual = entry.factualSummary && entry.factualSummary !== narrative
                ? `<span class="chronicle-facts">${escapeHtml(entry.factualSummary)}</span>`
                : "";
            return `
            <article class="item chronicle-entry">
                <strong>${escapeHtml(entry.title)} (${entry.importanceScore})</strong>
                <span>${escapeHtml(narrative)}</span>
                ${factual}
            </article>
        `;
        }).join("");
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
    syncTutorialDisplay();
}

async function selectFleet(fleetId) {
    state.selectedFleetId = fleetId;
    try {
        state.fleetDetail = await getJson(`/fleets/${fleetId}`);
        renderFleets(state.fleets);
        renderFleetDetails();
        syncTutorialDisplay();
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
        .replace("attack", "Attack")
        .replace("colonise", "Colonise");
}

function formatOrderTiming(order) {
    if (order.status === "pending") {
        return `executes after T${order.executeAfterTick}`;
    }

    if (order.status === "cancelled") {
        return order.processedTick === null ? "cancelled" : `cancelled T${order.processedTick}`;
    }

    return order.processedTick === null ? "processed" : `processed T${order.processedTick}`;
}

function fleetSelectLabel(item) {
    return item.admiral
        ? `${item.fleet.fleetName} - ${item.admiral.admiralName}`
        : item.fleet.fleetName;
}

function formatAdmiral(admiral) {
    return `${admiral.admiralName} (${formatNumber(admiral.reputationScore)} rep, ${formatStatus(admiral.status)})`;
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

function setTurnMessage(message) {
    elements.turnMessage.textContent = message;
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
    elements.priorityTotal.closest(".priority-total").classList.toggle("invalid", total !== 100);
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

function formatCount(value, singular, plural = `${singular}s`) {
    return `${formatNumber(value)} ${Number(value) === 1 ? singular : plural}`;
}

function resourceCard(label, value, maxResource, generated, spent) {
    const numeric = Number(value);
    const generatedNumeric = Number(generated ?? 0);
    const spentNumeric = Number(spent ?? 0);
    const width = numeric <= 0 ? 0 : Math.max(4, Math.round(numeric / maxResource * 100));
    return `
        <div class="resource-card">
            <dt>${escapeHtml(label)}</dt>
            <dd>
                <strong>${formatNumber(numeric)}</strong>
                <span class="resource-delta">Last tick +${formatNumber(generatedNumeric)} / -${formatNumber(spentNumeric)}</span>
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
