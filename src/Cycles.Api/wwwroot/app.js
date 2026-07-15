const state = {
    playerId: null,
    role: null,
    canAdvanceTurn: false,
    cycle: null,
    empire: null,
    galaxy: null,
    selectedSystemId: null,
    selectedSectorId: null,
    selectedFleetId: null,
    fleetDetail: null,
    fleets: [],
    orders: [],
    events: [],
    chronicle: [],
    openingBriefing: null,
    activeView: "command",
    fleetTab: "command",
    fleetAction: "move",
    historyTab: "chronicle",
    orderHistoryLimit: 20,
    orderHistoryScope: "selected",
    orderHistoryStatus: "all",
    orderHistorySort: "newest",
    chronicleQuery: "",
    chronicleMinImportance: 0,
    chronicleSort: "newest",
    eventQuery: "",
    eventSeverity: "all",
    eventSort: "newest",
    mapLens: "overview",
    mapPreset: "galaxy",
    mapViewBox: { x: 0, y: 0, width: 1000, height: 700 },
    mapDrag: null,
    mapDragged: false,
    mapRecentSystemIds: [],
    mapMaximised: false,
    priorityDraft: null,
    prioritySaving: false
};

const viewIds = ["command", "galaxy", "fleets", "history"];
const priorityKeys = ["industryWeight", "researchWeight", "militaryWeight", "expansionWeight"];
const inactivePriorityKeys = ["industryWeight", "researchWeight"];
const activePriorityKeys = ["militaryWeight", "expansionWeight"];
const mapBounds = Object.freeze({ x: 0, y: 0, width: 1000, height: 700 });
const mapPresetWidths = Object.freeze({
    galaxy: 1000,
    sector: 320,
    local: 180
});
const mapLensLabels = Object.freeze({
    overview: "Overview",
    presence: "Presence",
    strategy: "Strategy",
    output: "Output",
    history: "History"
});
const viewShortcuts = new Map([
    ["1", "command"],
    ["2", "galaxy"],
    ["3", "fleets"],
    ["4", "history"]
]);

const tutorial = {
    version: "v2",
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
let priorityActivityTimeout = null;

const elements = {
    loginForm: document.querySelector("#loginForm"),
    username: document.querySelector("#username"),
    loginButton: document.querySelector("#loginButton"),
    loginMessage: document.querySelector("#loginMessage"),
    sessionSummary: document.querySelector("#sessionSummary"),
    appHeaderControls: document.querySelector("#appHeaderControls"),
    sessionUsername: document.querySelector("#sessionUsername"),
    signOutButton: document.querySelector("#signOutButton"),
    appShell: document.querySelector("#appShell"),
    viewNav: document.querySelector("#viewNav"),
    views: [...document.querySelectorAll("[data-view]")],
    viewLinks: [...document.querySelectorAll("[data-view-link]")],
    commandViewBadge: document.querySelector("#commandViewBadge"),
    galaxyViewBadge: document.querySelector("#galaxyViewBadge"),
    fleetsViewBadge: document.querySelector("#fleetsViewBadge"),
    historyViewBadge: document.querySelector("#historyViewBadge"),
    cycleStatus: document.querySelector("#cycleStatus"),
    empireName: document.querySelector("#empireName"),
    homeSystemName: document.querySelector("#homeSystemName"),
    resources: document.querySelector("#resources"),
    systemDetails: document.querySelector("#systemDetails"),
    prioritySection: document.querySelector("#prioritySection"),
    priorityForm: document.querySelector("#priorityForm"),
    priorityInputs: [...document.querySelectorAll("[data-priority-key]")],
    priorityDraftStatus: document.querySelector("#priorityDraftStatus"),
    priorityResetButton: document.querySelector("#priorityResetButton"),
    prioritySaveButton: document.querySelector("#prioritySaveButton"),
    priorityMessage: document.querySelector("#priorityMessage"),
    fleets: document.querySelector("#fleetList"),
    fleetDetails: document.querySelector("#fleetDetails"),
    fleetTabs: document.querySelector("#fleetTabs"),
    fleetTabButtons: [...document.querySelectorAll("[data-fleet-tab]")],
    fleetTabPanels: [...document.querySelectorAll(".fleet-tab-panel")],
    fleetActionTabs: document.querySelector("#fleetActionTabs"),
    fleetActionButtons: [...document.querySelectorAll("[data-fleet-action]")],
    fleetActionPanels: [...document.querySelectorAll("[data-fleet-action-panel]")],
    selectedFleetActionName: document.querySelector("#selectedFleetActionName"),
    destinationSelect: document.querySelector("#destinationSelect"),
    targetEmpireSelect: document.querySelector("#targetEmpireSelect"),
    moveActionHint: document.querySelector("#moveActionHint"),
    attackActionHint: document.querySelector("#attackActionHint"),
    coloniseActionHint: document.querySelector("#coloniseActionHint"),
    moveForm: document.querySelector("#moveForm"),
    attackForm: document.querySelector("#attackForm"),
    coloniseForm: document.querySelector("#coloniseForm"),
    orderMessage: document.querySelector("#orderMessage"),
    orders: document.querySelector("#orders"),
    orderHistory: document.querySelector("#orderHistory"),
    orderHistoryCount: document.querySelector("#orderHistoryCount"),
    orderHistoryScope: document.querySelector("#orderHistoryScope"),
    orderHistoryStatus: document.querySelector("#orderHistoryStatus"),
    orderHistorySort: document.querySelector("#orderHistorySort"),
    events: document.querySelector("#events"),
    eventResultCount: document.querySelector("#eventResultCount"),
    eventSearch: document.querySelector("#eventSearch"),
    eventSeverity: document.querySelector("#eventSeverity"),
    eventSort: document.querySelector("#eventSort"),
    chronicle: document.querySelector("#chronicleEntries"),
    chronicleResultCount: document.querySelector("#chronicleResultCount"),
    chronicleSearch: document.querySelector("#chronicleSearch"),
    chronicleImportance: document.querySelector("#chronicleImportance"),
    chronicleSort: document.querySelector("#chronicleSort"),
    historyTabs: document.querySelector("#historyTabs"),
    historyTabButtons: [...document.querySelectorAll("[data-history-tab]")],
    historyTabPanels: [...document.querySelectorAll(".history-tab-panel")],
    systemHeading: document.querySelector("#systemHeading"),
    galaxyWorkspace: document.querySelector("#galaxyWorkspace"),
    systemSearchForm: document.querySelector("#systemSearchForm"),
    systemSearch: document.querySelector("#systemSearch"),
    systemOptions: document.querySelector("#systemOptions"),
    mapLensButtons: [...document.querySelectorAll("[data-map-lens]")],
    mapPresetButtons: [...document.querySelectorAll("[data-map-preset]")],
    mapInsightLabel: document.querySelector("#mapInsightLabel"),
    mapInsight: document.querySelector("#mapInsight"),
    mapOwnershipStats: document.querySelector("#mapOwnershipStats"),
    galaxyMap: document.querySelector("#galaxyMap"),
    mapNavigator: document.querySelector("#mapNavigator"),
    mapNavigatorReset: document.querySelector("#mapNavigatorReset"),
    mapRecentSystems: document.querySelector("#mapRecentSystems"),
    mapFocusHome: document.querySelector("#mapFocusHome"),
    mapFocusSelected: document.querySelector("#mapFocusSelected"),
    mapFocusFrontier: document.querySelector("#mapFocusFrontier"),
    mapMaximise: document.querySelector("#mapMaximise"),
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

    if (event.key === "Escape" && state.mapMaximised) {
        event.preventDefault();
        setMapMaximised(false);
        elements.mapMaximise.focus({ preventScroll: true });
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
    if (state.mapDragged) {
        state.mapDragged = false;
        return;
    }

    const node = event.target.closest(".system-node");
    if (node) {
        const entersSector = currentMapRange() === "galaxy" || node.classList.contains("is-adjacent-gateway");
        selectSystem(node.dataset.systemId, { focusMap: entersSector });
        return;
    }

    const sector = event.target.closest(".sector-node");
    if (sector) {
        focusMapOnSector(sector.dataset.sectorId);
    }
});

elements.galaxyMap.addEventListener("keydown", event => {
    if (event.key !== "Enter" && event.key !== " ") {
        return;
    }

    const node = event.target.closest(".system-node");
    if (node) {
        event.preventDefault();
        const entersSector = currentMapRange() === "galaxy" || node.classList.contains("is-adjacent-gateway");
        selectSystem(node.dataset.systemId, { focusMap: entersSector, restoreMapFocus: true });
        return;
    }

    const sector = event.target.closest(".sector-node");
    if (sector) {
        event.preventDefault();
        focusMapOnSector(sector.dataset.sectorId, { restoreMapFocus: true });
    }
});

elements.systemSearchForm.addEventListener("submit", event => {
    event.preventDefault();
    if (!state.galaxy) {
        return;
    }

    const query = elements.systemSearch.value.trim().toLowerCase();
    const match = state.galaxy.systems
        .slice()
        .sort((left, right) => left.systemName.localeCompare(right.systemName))
        .find(system => system.systemName.toLowerCase() === query)
        ?? state.galaxy.systems.find(system => system.systemName.toLowerCase().includes(query));

    const sectorMatch = normaliseGalaxySectors(state.galaxy)
        .slice()
        .sort((left, right) => left.sortOrder - right.sortOrder || left.sectorName.localeCompare(right.sectorName))
        .find(sector => mapSectorDisplayName(sector).toLowerCase() === query || sector.sectorName.toLowerCase() === query)
        ?? normaliseGalaxySectors(state.galaxy).find(sector => mapSectorDisplayName(sector).toLowerCase().includes(query));

    if (!match && !sectorMatch) {
        elements.systemSearch.setCustomValidity("Choose a known system or sector.");
        elements.systemSearch.reportValidity();
        return;
    }

    elements.systemSearch.setCustomValidity("");
    if (match) {
        selectSystem(match.systemId, { focusMap: true });
    } else {
        focusMapOnSector(sectorMatch.sectorId);
    }
    elements.galaxyMap.focus({ preventScroll: true });
});

elements.systemSearch.addEventListener("input", () => elements.systemSearch.setCustomValidity(""));

for (const button of elements.mapLensButtons) {
    button.addEventListener("click", () => setMapLens(button.dataset.mapLens));
}

for (const button of elements.mapPresetButtons) {
    button.addEventListener("click", () => applyMapPreset(button.dataset.mapPreset));
}

elements.mapFocusHome.addEventListener("click", () => recoverMapToSystem(state.empire?.homeSystem.systemId));
elements.mapFocusSelected.addEventListener("click", () => recoverMapToSystem(state.selectedSystemId));
elements.mapFocusFrontier.addEventListener("click", recoverMapToFrontier);
elements.mapNavigatorReset.addEventListener("click", () => recoverMapToSystem(state.selectedSystemId));
elements.mapMaximise.addEventListener("click", () => setMapMaximised(!state.mapMaximised));

elements.mapRecentSystems.addEventListener("click", event => {
    const button = event.target.closest("[data-recent-system]");
    if (button) {
        selectSystem(button.dataset.recentSystem, { focusMap: true });
    }
});

elements.mapNavigator.addEventListener("click", moveMapFromNavigator);
elements.mapNavigator.addEventListener("keydown", event => {
    if (["ArrowLeft", "ArrowUp", "ArrowRight", "ArrowDown"].includes(event.key)) {
        event.preventDefault();
        const target = directionalMapSector(state.selectedSectorId, event.key);
        if (target) {
            focusMapOnSector(target.sectorId);
        }
        return;
    }

    if (event.key !== "Enter" && event.key !== " ") {
        return;
    }

    event.preventDefault();
    focusMapOnSector(state.selectedSectorId);
});

elements.galaxyMap.addEventListener("pointerdown", event => {
    if (event.button !== 0 || event.target.closest(".system-node, .sector-node")) {
        return;
    }

    state.mapDrag = {
        clientX: event.clientX,
        clientY: event.clientY,
        viewBox: { ...state.mapViewBox }
    };
    state.mapDragged = false;
    elements.galaxyMap.classList.add("is-panning");
    elements.galaxyMap.setPointerCapture(event.pointerId);
});

elements.galaxyMap.addEventListener("pointermove", event => {
    if (!state.mapDrag) {
        return;
    }

    const rect = elements.galaxyMap.getBoundingClientRect();
    const deltaX = event.clientX - state.mapDrag.clientX;
    const deltaY = event.clientY - state.mapDrag.clientY;
    if (Math.abs(deltaX) + Math.abs(deltaY) > 4) {
        state.mapDragged = true;
    }

    state.mapViewBox.x = state.mapDrag.viewBox.x - deltaX * state.mapDrag.viewBox.width / rect.width;
    state.mapViewBox.y = state.mapDrag.viewBox.y - deltaY * state.mapDrag.viewBox.height / rect.height;
    constrainMapViewBox();
    applyMapViewBox();
});

for (const eventName of ["pointerup", "pointercancel"]) {
    elements.galaxyMap.addEventListener(eventName, event => {
        if (!state.mapDrag) {
            return;
        }

        state.mapDrag = null;
        elements.galaxyMap.classList.remove("is-panning");
        if (elements.galaxyMap.hasPointerCapture(event.pointerId)) {
            elements.galaxyMap.releasePointerCapture(event.pointerId);
        }
        if (eventName === "pointercancel") {
            state.mapDragged = false;
        } else {
            if (state.mapDragged) {
                syncMapSectorContextToCamera();
            }
            window.setTimeout(() => {
                state.mapDragged = false;
            }, 0);
        }
    });
}

elements.systemDetails.addEventListener("click", async event => {
    const systemButton = event.target.closest("[data-focus-system]");
    if (systemButton) {
        selectSystem(systemButton.dataset.focusSystem, { focusMap: true });
        elements.galaxyMap.focus({ preventScroll: true });
        return;
    }

    const fleetButton = event.target.closest("[data-command-fleet]");
    if (fleetButton) {
        await selectFleet(fleetButton.dataset.commandFleet);
        activateView("fleets", { updateLocation: true, focusHeading: true });
    }
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

bindTabList(elements.fleetTabs, elements.fleetTabButtons, "fleetTab", activateFleetTab);
bindTabList(elements.fleetActionTabs, elements.fleetActionButtons, "fleetAction", activateFleetAction);
bindTabList(elements.historyTabs, elements.historyTabButtons, "historyTab", activateHistoryTab);

elements.orderHistoryScope.addEventListener("change", () => {
    state.orderHistoryScope = elements.orderHistoryScope.value;
    state.orderHistoryLimit = 20;
    renderOrderHistory();
});

elements.orderHistoryStatus.addEventListener("change", () => {
    state.orderHistoryStatus = elements.orderHistoryStatus.value;
    state.orderHistoryLimit = 20;
    renderOrderHistory();
});

elements.orderHistorySort.addEventListener("change", () => {
    state.orderHistorySort = elements.orderHistorySort.value;
    state.orderHistoryLimit = 20;
    renderOrderHistory();
});

elements.chronicleSearch.addEventListener("input", () => {
    state.chronicleQuery = elements.chronicleSearch.value.trim().toLowerCase();
    renderChronicle(state.chronicle);
});

elements.chronicleImportance.addEventListener("change", () => {
    state.chronicleMinImportance = Number(elements.chronicleImportance.value);
    renderChronicle(state.chronicle);
});

elements.chronicleSort.addEventListener("change", () => {
    state.chronicleSort = elements.chronicleSort.value;
    renderChronicle(state.chronicle);
});

elements.eventSearch.addEventListener("input", () => {
    state.eventQuery = elements.eventSearch.value.trim().toLowerCase();
    renderEvents(state.events);
});

elements.eventSeverity.addEventListener("change", () => {
    state.eventSeverity = elements.eventSeverity.value;
    renderEvents(state.events);
});

elements.eventSort.addEventListener("change", () => {
    state.eventSort = elements.eventSort.value;
    renderEvents(state.events);
});

elements.priorityForm.addEventListener("input", event => {
    const input = event.target.closest("[data-priority-key]");
    if (!input || !state.priorityDraft) {
        return;
    }

    rebalancePriorityDraft(input.dataset.priorityKey, parseWeight(input.value));
    renderPriorityControls();
    setPriorityMessage("");
    pulsePriorityConsole();
});

elements.priorityResetButton.addEventListener("click", () => {
    if (!state.empire) {
        return;
    }

    renderPriorities(state.empire.priorities);
    setPriorityMessage("Allocation reset to the saved values.");
});

elements.priorityForm.addEventListener("submit", async event => {
    event.preventDefault();
    setPriorityMessage("");
    if (!state.empire) {
        setPriorityMessage("Login before updating priorities.");
        return;
    }

    const payload = { ...state.priorityDraft };
    const isDirty = priorityKeys.some(key => payload[key] !== parseWeight(state.empire.priorities[key]));

    if (Object.values(payload).reduce((total, value) => total + value, 0) !== 100) {
        setPriorityMessage("Priorities must total 100.");
        return;
    }

    if (!isDirty) {
        return;
    }

    state.prioritySaving = true;
    renderPriorityControls();
    try {
        await postJson("/orders/priorities", payload);
        await refresh();
        setPriorityMessage("Priorities saved for the next tick.");
        completeTutorialAction("prioritiesSaved");
    } catch (error) {
        setPriorityMessage(error.message);
    } finally {
        state.prioritySaving = false;
        renderPriorityControls();
    }
});

elements.moveForm.addEventListener("submit", async event => {
    event.preventDefault();
    const fleetId = state.selectedFleetId;
    const targetSystemId = elements.destinationSelect.value;
    if (!fleetId || !targetSystemId) {
        setMessage("Select an active fleet with a linked destination.");
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
    const fleetId = state.selectedFleetId;
    const targetEmpireId = elements.targetEmpireSelect.value || null;
    if (!fleetId) {
        setMessage("Select an active fleet before attacking.");
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
    const fleetId = state.selectedFleetId;
    if (!fleetId) {
        setMessage("Select an active fleet outside its home system.");
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
    renderOrderHistory();
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
    window.location.assign("/auth/logout");
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
    elements.appHeaderControls.hidden = false;
    elements.appShell.hidden = false;
    activateView(resolveInitialView(), { updateLocation: true });
    activateFleetTab(state.fleetTab);
    activateFleetAction(state.fleetAction);
    activateHistoryTab(state.historyTab);
}

function showLogin(message) {
    if (state.playerId) {
        resetTutorialContext();
    }

    state.playerId = null;
    state.role = null;
    state.canAdvanceTurn = false;
    state.empire = null;
    setMapMaximised(false);
    elements.loginMessage.textContent = message;
    elements.loginForm.hidden = false;
    elements.sessionSummary.hidden = true;
    elements.appHeaderControls.hidden = true;
    elements.appShell.hidden = true;
}

async function refresh() {
    const [cycle, empire, galaxy, fleets, orders, events, chronicle, openingBriefing] = await Promise.all([
        getJson("/cycles/current"),
        getJson("/empire"),
        getJson("/galaxy"),
        getJson("/fleets"),
        getJson("/orders"),
        getJson("/events/recent?limit=20"),
        getJson("/chronicle"),
        getJson("/briefings/opening")
    ]);

    state.empire = empire;
    state.cycle = cycle;
    state.galaxy = galaxy;
    state.fleets = fleets;
    state.orders = orders;
    state.events = events;
    state.chronicle = chronicle;
    state.openingBriefing = openingBriefing;

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
    rememberMapSystem(state.selectedSystemId);

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
    renderViewBadges();
    syncTutorialAfterRefresh();
}

function viewFromHash() {
    const value = window.location.hash.slice(1).toLowerCase();
    if (value === "chronicle") {
        return "history";
    }

    return viewIds.includes(value) ? value : null;
}

function resolveInitialView() {
    const requestedView = viewFromHash();
    if (requestedView) {
        return requestedView;
    }

    const storedValue = readStoredValue("cycles.activeView");
    const storedView = storedValue === "chronicle" ? "history" : storedValue;
    return viewIds.includes(storedView) ? storedView : "command";
}

function activateView(viewId, { updateLocation = false, focusHeading = false } = {}) {
    const selectedView = viewIds.includes(viewId) ? viewId : "command";
    if (selectedView !== "galaxy" && state.mapMaximised) {
        setMapMaximised(false);
    }
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

function bindTabList(container, buttons, dataKey, activate) {
    container.addEventListener("click", event => {
        const button = event.target.closest("[role=tab]");
        if (button) {
            activate(button.dataset[dataKey]);
        }
    });

    container.addEventListener("keydown", event => {
        if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) {
            return;
        }

        event.preventDefault();
        const currentIndex = Math.max(0, buttons.indexOf(document.activeElement));
        const nextIndex = event.key === "Home"
            ? 0
            : event.key === "End"
                ? buttons.length - 1
                : (currentIndex + (event.key === "ArrowRight" ? 1 : -1) + buttons.length) % buttons.length;
        activate(buttons[nextIndex].dataset[dataKey], { focusTab: true });
    });
}

function activateFleetTab(tabId, { focusTab = false } = {}) {
    const selected = ["command", "history"].includes(tabId) ? tabId : "command";
    state.fleetTab = selected;
    syncTabSet(elements.fleetTabButtons, elements.fleetTabPanels, "fleetTab", selected);
    if (focusTab) {
        elements.fleetTabButtons.find(button => button.dataset.fleetTab === selected)?.focus();
    }
}

function activateFleetAction(actionId, { focusTab = false } = {}) {
    const selected = ["move", "attack", "colonise"].includes(actionId) ? actionId : "move";
    state.fleetAction = selected;
    syncTabSet(elements.fleetActionButtons, elements.fleetActionPanels, "fleetAction", selected);
    if (focusTab) {
        elements.fleetActionButtons.find(button => button.dataset.fleetAction === selected)?.focus();
    }
}

function activateHistoryTab(tabId, { focusTab = false } = {}) {
    const selected = ["chronicle", "events"].includes(tabId) ? tabId : "chronicle";
    state.historyTab = selected;
    syncTabSet(elements.historyTabButtons, elements.historyTabPanels, "historyTab", selected);
    if (focusTab) {
        elements.historyTabButtons.find(button => button.dataset.historyTab === selected)?.focus();
    }
}

function syncTabSet(buttons, panels, dataKey, selected) {
    for (const button of buttons) {
        const active = button.dataset[dataKey] === selected;
        button.setAttribute("aria-selected", String(active));
        button.tabIndex = active ? 0 : -1;
    }

    for (const panel of panels) {
        panel.hidden = panel.dataset[`${dataKey}Panel`] !== selected;
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
    elements.homeSystemName.textContent = empire.homeSystem.systemName;
    const resources = empire.resources;
    const maxResource = Math.max(1, Number(resources.industry), Number(resources.research), Number(resources.population));
    elements.resources.innerHTML = `
        ${resourceCard("Industry", resources.industry, maxResource, resources.lastGeneratedIndustry, resources.lastSpentIndustry)}
        ${resourceCard("Research", resources.research, maxResource, resources.lastGeneratedResearch, resources.lastSpentResearch)}
        ${resourceCard("Population", resources.population, maxResource, resources.lastGeneratedPopulation, resources.lastSpentPopulation)}
    `;
}

function renderViewBadges() {
    const pendingOrders = state.orders.filter(order => order.status === "pending").length;
    const activeFleets = state.fleets.filter(item => item.fleet.status === "active" && item.fleet.shipCount > 0).length;
    const visibleEvents = state.events.length;
    const chronicleEntries = state.chronicle.length;

    setViewBadge(elements.commandViewBadge, pendingOrders, `${formatCount(pendingOrders, "pending order")}`);
    setViewBadge(elements.galaxyViewBadge, state.galaxy?.systems.length ?? 0, `${formatCount(state.galaxy?.systems.length ?? 0, "system")}`);
    setViewBadge(elements.fleetsViewBadge, activeFleets, `${formatCount(activeFleets, "active fleet")}`);
    setViewBadge(elements.historyViewBadge, visibleEvents + chronicleEntries, `${formatCount(visibleEvents + chronicleEntries, "historical record")}`);
}

function setViewBadge(element, value, label) {
    element.textContent = formatNumber(value);
    element.setAttribute("aria-label", label);
}

function renderPriorities(priorities) {
    const normalised = normalisePriorityAllocation(priorities);
    if (state.empire) {
        state.empire.priorities = { ...priorities, ...normalised };
    }

    state.priorityDraft = normalised;
    renderPriorityControls();
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

    const pendingOrders = detail.orders.filter(order => order.status === "pending");
    const resolvedOrderCount = detail.orders.length - pendingOrders.length;
    const orders = pendingOrders.length === 0
        ? `<span>No pending intention. This fleet is ready for a command.</span>`
        : pendingOrders.map(order => {
            const target = order.targetSystemName ?? order.targetEmpireName ?? "nearest hostile";
            const timing = formatOrderTiming(order);
            return `<span>${escapeHtml(formatOrderType(order.orderType))} | ${escapeHtml(target)} | ${escapeHtml(timing)}</span>`;
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
            <strong>Current intention</strong>
            ${orders}
        </div>
        <p class="fleet-history-summary">${formatCount(resolvedOrderCount, "resolved order")} recorded for this fleet.</p>
    `;
}

function renderOrders() {
    const activeFleets = state.fleets.filter(item => item.fleet.status === "active" && item.fleet.shipCount > 0);
    const selectedFleet = activeFleets.find(item => item.fleet.fleetId === state.selectedFleetId) ?? null;
    elements.selectedFleetActionName.textContent = selectedFleet?.fleet.fleetName ?? "selected fleet";

    const destinations = selectedFleet ? linkedSystems(selectedFleet.fleet.currentSystemId) : [];
    fillSelect(elements.destinationSelect, destinations, item => item.systemId, item => item.systemName);
    if (destinations.length === 0) {
        elements.destinationSelect.innerHTML = `<option value="">No linked destinations</option>`;
    }

    const targetEmpires = collectTargetEmpires(selectedFleet);
    fillSelect(elements.targetEmpireSelect, targetEmpires, item => item.empireId, item => item.empireName, true);

    const fleetReady = Boolean(selectedFleet);
    const awayFromHome = fleetReady && selectedFleet.fleet.currentSystemId !== state.empire.homeSystem.systemId;
    elements.moveForm.querySelector("button[type=submit]").disabled = !fleetReady || destinations.length === 0;
    elements.attackForm.querySelector("button[type=submit]").disabled = !fleetReady;
    elements.coloniseForm.querySelector("button[type=submit]").disabled = !awayFromHome;

    elements.moveActionHint.textContent = !fleetReady
        ? "Select an active fleet to see available routes."
        : destinations.length === 0
            ? "This fleet has no linked destination available."
            : `${formatCount(destinations.length, "adjacent destination")} available from ${selectedFleet.currentSystemName}.`;
    elements.attackActionHint.textContent = !fleetReady
        ? "Select an active fleet to prepare an attack."
        : targetEmpires.length === 0
            ? "No visible local rival; the order will target the nearest hostile empire."
            : `${formatCount(targetEmpires.length, "visible local rival")} available, or choose nearest hostile.`;
    elements.coloniseActionHint.textContent = !fleetReady
        ? "Select an active fleet to assess colonisation."
        : !awayFromHome
            ? "Move this fleet beyond its home system before establishing an outpost."
            : "Costs 100 population and resolves next tick.";
}

function renderSystemDetails() {
    if (!state.galaxy || !state.selectedSystemId) {
        elements.systemHeading.textContent = "System";
        elements.systemDetails.innerHTML = `<article class="item"><span>No system selected.</span></article>`;
        return;
    }

    const system = state.galaxy.systems.find(item => item.systemId === state.selectedSystemId);
    if (!system) {
        elements.systemHeading.textContent = "System";
        elements.systemDetails.innerHTML = `<article class="item"><span>No system selected.</span></article>`;
        return;
    }

    const presence = state.galaxy.presence.find(item => item.systemId === system.systemId)?.effectivePresence ?? {};
    const presenceEntries = Object.entries(presence)
        .filter(([, value]) => Number(value) > 0)
        .sort((first, second) => Number(second[1]) - Number(first[1]));
    const presenceMaximum = Math.max(1, ...presenceEntries.map(([, value]) => Number(value)));
    const presenceRows = presenceEntries
        .map(([empireId, value]) => {
            const isOwn = empireId === state.empire.empireId;
            const label = isOwn ? state.empire.empireName : `Rival signal ${empireId.slice(0, 5)}`;
            const width = Math.max(5, Number(value) / presenceMaximum * 100);
            return `
                <div class="presence-row${isOwn ? " is-own" : " is-rival"}">
                    <div><span>${escapeHtml(label)}</span><strong>${formatNumber(value)}</strong></div>
                    <span class="presence-meter"><i style="width: ${width}%"></i></span>
                </div>
            `;
        }).join("");
    const outposts = state.galaxy.colonialOutposts
        .filter(item => item.systemId === system.systemId)
        .map(item => `
            <span class="outpost-record">
                <strong>${escapeHtml(item.empireName)}</strong>
                <small>Established T${item.establishedTick} | ${item.isProjectingPresence ? "projecting presence" : "inactive"}</small>
            </span>
        `)
        .join("");
    const routes = linkedSystems(system.systemId);
    const routeButtons = routes.length === 0
        ? `<span class="system-empty-note">No adjacent routes.</span>`
        : routes.map(linked => `
            <button type="button" class="route-jump" data-focus-system="${linked.systemId}">
                <span>${escapeHtml(linked.systemName)}</span>
                <small>${linked.sectorId !== system.sectorId ? "Sector gate | " : ""}${formatCount(linked.routeTravelTicks, "tick")} | Strategic ${formatNumber(linked.strategicValue)}</small>
            </button>
        `).join("");
    const localFleets = state.fleets
        .filter(item => item.fleet.currentSystemId === system.systemId && item.fleet.status === "active" && item.fleet.shipCount > 0)
        .sort((left, right) => left.fleet.fleetName.localeCompare(right.fleet.fleetName));
    const fleetButtons = localFleets.length === 0
        ? `<span class="system-empty-note">No commandable fleet stationed here.</span>`
        : localFleets.map(item => `
            <button type="button" class="fleet-jump" data-command-fleet="${item.fleet.fleetId}">
                <span>${escapeHtml(item.fleet.fleetName)}</span>
                <small>${formatCount(item.fleet.shipCount, "ship")} | Open command</small>
            </button>
        `).join("");
    const activePresence = presenceEntries.length;
    const ownPresence = Number(presence[state.empire.empireId] ?? 0);
    const sector = normaliseGalaxySectors(state.galaxy).find(candidate => candidate.sectorId === system.sectorId);
    const sectorLabel = sector ? mapSectorDisplayName(sector) : "Uncharted";
    const tags = [
        sectorLabel,
        system.isGateway ? "Sector gateway" : null,
        system.systemId === state.empire.homeSystem.systemId ? "Home system" : null,
        activePresence > 1 ? "Contested" : null,
        system.historicalSignificance > 0 ? "Historic" : null,
        ownPresence > 0 ? "Active presence" : "No visible presence",
        formatCount(routes.length, "route")
    ].filter(Boolean);
    const yields = [
        ["Industry", system.industryOutput, "industry"],
        ["Research", system.researchOutput, "research"],
        ["Population", system.populationOutput, "population"]
    ];
    const maximumYield = Math.max(1, ...yields.map(([, value]) => Number(value)));
    const yieldCards = yields.map(([label, value, key]) => `
        <div class="system-yield system-yield-${key}">
            <span>${label}</span>
            <strong>${formatNumber(value)}</strong>
            <i style="--yield-width: ${Math.max(4, Number(value) / maximumYield * 100)}%"></i>
        </div>
    `).join("");

    elements.systemHeading.textContent = system.systemName;
    elements.systemDetails.innerHTML = `
        <section class="system-overview" aria-label="System overview">
            <div class="system-signature">
                <span>${escapeHtml(sectorLabel)} | Grid ${formatNumber(system.x)} / ${formatNumber(system.y)}</span>
                <button type="button" class="map-focus-action" data-focus-system="${system.systemId}">Focus map</button>
            </div>
            <div class="system-rating">
                <span><small>Strategic value</small><strong>${formatNumber(system.strategicValue)}</strong></span>
                <span><small>Historical signal</small><strong>${formatNumber(system.historicalSignificance)}</strong></span>
            </div>
            <div class="system-tags">${tags.map(tag => `<span>${escapeHtml(tag)}</span>`).join("")}</div>
        </section>

        <section class="system-intel-block" aria-labelledby="systemOutputHeading">
            <div class="system-block-heading">
                <span class="section-kicker">Local capacity</span>
                <h3 id="systemOutputHeading">System output</h3>
            </div>
            <div class="system-yields">${yieldCards}</div>
        </section>

        <section class="system-intel-block" aria-labelledby="systemPresenceHeading">
            <div class="system-block-heading">
                <span class="section-kicker">Visible control</span>
                <h3 id="systemPresenceHeading">Presence</h3>
            </div>
            <div class="presence-chart">${presenceRows || `<span class="system-empty-note">No visible presence.</span>`}</div>
        </section>

        <section class="system-intel-block" aria-labelledby="systemRoutesHeading">
            <div class="system-block-heading">
                <span class="section-kicker">Immediate reach</span>
                <h3 id="systemRoutesHeading">Linked routes</h3>
            </div>
            <div class="route-list">${routeButtons}</div>
        </section>

        <section class="system-intel-block" aria-labelledby="systemFleetsHeading">
            <div class="system-block-heading">
                <span class="section-kicker">Local command</span>
                <h3 id="systemFleetsHeading">Your fleets</h3>
            </div>
            <div class="route-list">${fleetButtons}</div>
        </section>

        <section class="system-intel-block" aria-labelledby="systemOutpostsHeading">
            <div class="system-block-heading">
                <span class="section-kicker">Expansion</span>
                <h3 id="systemOutpostsHeading">Colonial outposts</h3>
            </div>
            <div class="outpost-list">${outposts || `<span class="system-empty-note">None established.</span>`}</div>
        </section>
    `;
}

function renderOrderQueue(orders) {
    const pendingOrders = orders.filter(order => order.status === "pending");
    elements.orders.innerHTML = pendingOrders.length === 0
        ? `<article class="item empty-state"><strong>No pending orders</strong><span>Issue an order when you are ready to commit the next turn.</span></article>`
        : pendingOrders.map(order => orderCard(order, true)).join("");

    renderOrderHistory();
}

function renderOrderHistory() {
    const allResolvedOrders = state.orders.filter(order => order.status !== "pending");
    const filteredOrders = allResolvedOrders
        .filter(order => state.orderHistoryScope === "all" || order.fleetId === state.selectedFleetId)
        .filter(order => state.orderHistoryStatus === "all" || order.status.toLowerCase() === state.orderHistoryStatus)
        .sort((left, right) => {
            const direction = state.orderHistorySort === "oldest" ? 1 : -1;
            return direction * (orderHistoryTick(left) - orderHistoryTick(right))
                || direction * left.fleetOrderId.localeCompare(right.fleetOrderId);
        });
    const visibleOrders = filteredOrders.slice(0, state.orderHistoryLimit);
    const remaining = filteredOrders.length - visibleOrders.length;
    const loadMore = remaining > 0
        ? `<button type="button" class="history-load-more" data-load-more-orders>Show ${formatCount(Math.min(20, remaining), "more order")}</button>`
        : "";

    elements.orderHistoryCount.textContent = allResolvedOrders.length === 0
        ? "No resolved orders"
        : `Showing ${formatNumber(visibleOrders.length)} of ${formatNumber(filteredOrders.length)} matches`;
    elements.orderHistory.innerHTML = filteredOrders.length === 0
        ? `<article class="item empty-state"><strong>No matching orders</strong><span>Adjust the scope or outcome filter to widen the history.</span></article>`
        : `${visibleOrders.map(order => orderCard(order, false)).join("")}${loadMore}`;
}

function orderHistoryTick(order) {
    return Number(order.processedTick ?? order.executeAfterTick ?? 0);
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
        tutorial.briefing = state.openingBriefing;
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
        tutorial.briefing = state.openingBriefing ?? tutorial.briefing;
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
    if (step.fleetTab) {
        activateFleetTab(step.fleetTab);
    }
    if (step.fleetAction) {
        activateFleetAction(step.fleetAction);
    }
    if (step.historyTab) {
        activateHistoryTab(step.historyTab);
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
            body: "The 100 points represent strategic effort, not the three resource stockpiles. Development and Innovation are locked at zero until their programmes are active. Military converts Industry into ship construction; Expansion strengthens projected influence. Adjust either active slider and save the new allocation when it is ready.",
            target: () => document.querySelector("#prioritySection"),
            required: true,
            requirement: "Save the priority allocation to continue.",
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
            id: "visibility",
            view: "galaxy",
            title: "Know what the map does not reveal",
            body: "You always see the galaxy topology and routes. Exact remote presence, fleets, events, last-turn facts, and Chronicle detail appear only where your active fleets provide visibility; an apparently quiet system may still contain hidden activity.",
            target: () => elements.galaxyMap,
            required: false
        },
        {
            id: "fleet",
            view: "fleets",
            fleetTab: "command",
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
            fleetTab: "command",
            fleetAction: "move",
            title: curated ? "Secure Nadir Crossing" : "Commit a movement order",
            body: curated
                ? `Select ${tutorialFleetName(moveFleetId)} in the roster. This guide opens Move; choose ${tutorialSystemName(moveTargetId)}, then queue the order. The server validates the intention again when the turn resolves.`
                : `Select ${tutorialFleetName(moveFleetId)} in the roster. This guide opens Move; choose ${tutorialSystemName(moveTargetId)}, then queue the order for the next authoritative turn.`,
            target: () => state.selectedFleetId === moveFleetId
                ? document.querySelector("#moveForm")
                : document.querySelector(`[data-fleet-id="${moveFleetId}"]`),
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
                fleetTab: "command",
                fleetAction: "colonise",
                title: "Establish the Pale Harbour outpost",
                body: `Select ${tutorialFleetName(colonise.fleetId)} in the roster. This guide opens Colonise; queue the outpost from that fleet. It costs 100 population and succeeds because the fleet has the leading local influence.`,
                target: () => state.selectedFleetId === colonise.fleetId
                    ? document.querySelector("#coloniseForm")
                    : document.querySelector(`[data-fleet-id="${colonise.fleetId}"]`),
                required: true,
                requirement: "Queue the Pale Harbour outpost.",
                isSatisfied: () => tutorialOrderExists("colonise", colonise.fleetId)
            },
            {
                id: "attack",
                view: "fleets",
                fleetTab: "command",
                fleetAction: "attack",
                title: "Answer the Khepri challenge",
                body: `Select ${tutorialFleetName(attack.fleetId)} in the roster. This guide opens Attack; choose the local Khepri force, then queue the order. Combat is deterministic from persisted facts, but victory is not scripted. Treaty Gate is important enough that the result will enter the Chronicle.`,
                target: () => state.selectedFleetId === attack.fleetId
                    ? document.querySelector("#attackForm")
                    : document.querySelector(`[data-fleet-id="${attack.fleetId}"]`),
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
            view: "command",
            title: "Resolve the turn",
            body: "Advance turn runs the same authoritative simulation boundary as the Worker and CLI. It resolves the whole development galaxy, not only your empire.",
            target: () => elements.advanceTurnButton,
            required: true,
            requirement: "Advance the development galaxy by one turn.",
            isSatisfied: () => state.cycle?.currentTickNumber > tutorial.initialTick
        },
        {
            id: "events",
            view: "history",
            historyTab: "events",
            title: "Read what actually happened",
            body: "Events are the factual audit trail. Check which orders processed, what resources changed, and whether anything was rejected when the world moved underneath an intention.",
            target: () => document.querySelector("#eventsSection"),
            required: false
        }
    );

    steps.push({
        id: "chronicle",
        view: "history",
        historyTab: "chronicle",
        title: "See what became history",
        body: curated
            ? "The Chronicle preserves exceptional events, not every routine action. Treaty Gate appears here because its battle crossed the importance threshold using real losses, strategy, and prior history."
            : "The Chronicle is selective history, not a second audit log. Only visible events important enough to cross the historical threshold appear here.",
        target: () => document.querySelector("#chronicleSection"),
        required: false
    });

    steps.push({
        id: "cycle-history",
        view: "history",
        historyTab: "events",
        title: "Place this turn in the Cycle",
        body: `You are viewing tick ${state.cycle?.currentTickNumber ?? 0} of the current Cycle. Events record factual turn results; the Chronicle preserves selected history. In this build, an operator ends the Cycle, records the final ranking, and creates its successor outside the player dashboard.`,
        target: () => elements.cycleStatus,
        required: false
    });

    steps.push({
        id: "next",
        view: "command",
        title: "That is the Cycles loop",
        body: "Inspect, prioritise, commit orders, resolve the turn, then read the visible consequences. From here, reinforce pressure, build ships, found outposts, or seek another battle worth remembering before the operator closes the Cycle.",
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
    const filteredEvents = events
        .filter(event => state.eventSeverity === "all" || event.severity.toLowerCase() === state.eventSeverity)
        .filter(event => {
            if (!state.eventQuery) {
                return true;
            }

            return [event.displayText, event.eventType, event.severity]
                .some(value => String(value ?? "").toLowerCase().includes(state.eventQuery));
        })
        .sort((left, right) => {
            if (state.eventSort === "severity-desc") {
                return eventSeverityRank(right.severity) - eventSeverityRank(left.severity)
                    || right.tickNumber - left.tickNumber;
            }

            const direction = state.eventSort === "oldest" ? 1 : -1;
            return direction * (left.tickNumber - right.tickNumber)
                || direction * String(left.createdAt).localeCompare(String(right.createdAt));
        });

    elements.eventResultCount.textContent = `${formatNumber(filteredEvents.length)} of ${formatCount(events.length, "event")}`;
    elements.events.innerHTML = filteredEvents.length === 0
        ? events.length === 0
            ? `<article class="item empty-state"><strong>No events yet</strong><span>Events will appear after the galaxy advances.</span></article>`
            : `<article class="item empty-state"><strong>No matching events</strong><span>Adjust the search, severity, or sort controls.</span></article>`
        : filteredEvents.map(event => `
            <article class="item event-entry">
                <header class="history-entry-header">
                    <span class="history-tick">T${event.tickNumber}</span>
                    <strong>${escapeHtml(formatStatus(event.eventType))}</strong>
                    <span class="status-chip status-${statusClass(event.severity)}">${escapeHtml(formatStatus(event.severity))}</span>
                </header>
                <p>${escapeHtml(event.displayText)}</p>
            </article>
        `).join("");
}

function renderChronicle(entries) {
    const filteredEntries = entries
        .filter(entry => Number(entry.importanceScore) >= state.chronicleMinImportance)
        .filter(entry => {
            if (!state.chronicleQuery) {
                return true;
            }

            return [entry.title, entry.factualSummary, entry.narrativeText, entry.entryType]
                .some(value => String(value ?? "").toLowerCase().includes(state.chronicleQuery));
        })
        .sort((left, right) => {
            if (state.chronicleSort === "importance-desc" || state.chronicleSort === "importance-asc") {
                const direction = state.chronicleSort === "importance-asc" ? 1 : -1;
                return direction * (left.importanceScore - right.importanceScore)
                    || (right.tickNumber ?? 0) - (left.tickNumber ?? 0);
            }

            const direction = state.chronicleSort === "oldest" ? 1 : -1;
            return direction * ((left.tickNumber ?? 0) - (right.tickNumber ?? 0))
                || direction * String(left.createdAt).localeCompare(String(right.createdAt));
        });

    elements.chronicleResultCount.textContent = `${formatNumber(filteredEntries.length)} of ${formatCount(entries.length, "entry", "entries")}`;
    elements.chronicle.innerHTML = filteredEntries.length === 0
        ? entries.length === 0
            ? `<article class="item empty-state"><strong>No Chronicle entries yet</strong><span>Exceptional events will be preserved here when they cross the importance threshold.</span></article>`
            : `<article class="item empty-state"><strong>No matching Chronicle entries</strong><span>Adjust the search, importance, or sort controls.</span></article>`
        : filteredEntries.map(entry => {
            const narrative = entry.narrativeText || "";
            const narrativeMarkup = narrative && narrative !== entry.factualSummary
                ? `<p class="chronicle-narrative">${escapeHtml(narrative)}</p>`
                : "";
            return `
            <article class="item chronicle-entry">
                <header class="history-entry-header chronicle-entry-title">
                    <span class="history-tick">T${entry.tickNumber ?? "?"}</span>
                    <strong>${escapeHtml(entry.title)}</strong>
                    <span class="importance-chip">Importance ${formatNumber(entry.importanceScore)}</span>
                </header>
                <p class="chronicle-summary">${escapeHtml(entry.factualSummary)}</p>
                ${narrativeMarkup}
            </article>
        `;
        }).join("");
}

function eventSeverityRank(value) {
    return ({ low: 1, normal: 2, high: 3, historic: 4 })[String(value).toLowerCase()] ?? 0;
}

function renderGalaxy(galaxy, empire) {
    const systems = new Map(galaxy.systems.map(system => [system.systemId, system]));
    const sectors = normaliseGalaxySectors(galaxy);
    const sectorsById = new Map(sectors.map(sector => [sector.sectorId, sector]));
    const presenceBySystem = new Map(galaxy.presence.map(item => [item.systemId, item.effectivePresence]));
    const homeId = empire.homeSystem.systemId;
    const selectedId = state.selectedSystemId;
    const selectedSystem = systems.get(selectedId);
    if (selectedSystem?.sectorId) {
        state.selectedSectorId = selectedSystem.sectorId;
    }
    if (!state.selectedSectorId || !sectorsById.has(state.selectedSectorId)) {
        state.selectedSectorId = sectorsById.has(empire.homeSystem.sectorId)
            ? empire.homeSystem.sectorId
            : sectors[0]?.sectorId ?? null;
    }

    const sectorContext = mapSectorContext(galaxy, state.selectedSectorId);
    const composition = mapComposition(galaxy);
    const sectorLayer = renderMapSectorLayer(galaxy, sectors, sectorContext, composition);
    const lines = galaxy.links.map(link => {
        const a = systems.get(link.systemAId);
        const b = systems.get(link.systemBId);
        if (!a || !b) {
            return "";
        }

        const isSelectedRoute = link.systemAId === selectedId || link.systemBId === selectedId;
        const isBridge = a.sectorId !== b.sectorId;
        const isActiveSectorRoute = a.sectorId === state.selectedSectorId && b.sectorId === state.selectedSectorId;
        const isGatewayContext = isBridge && (a.sectorId === state.selectedSectorId || b.sectorId === state.selectedSectorId);
        const isInComposition = composition.linkIds.has(mapLinkKey(link));
        const classes = [
            "route-segment",
            isInComposition ? "is-in-composition" : "",
            isSelectedRoute ? "is-selected" : "",
            isBridge ? "is-bridge" : "is-local-route",
            isActiveSectorRoute ? "is-active-sector" : "",
            isGatewayContext ? "is-gateway-context" : ""
        ].filter(Boolean).join(" ");
        return `
            <g class="${classes}">
                <line class="route-glow" x1="${a.x}" y1="${a.y}" x2="${b.x}" y2="${b.y}"></line>
                <line class="link${isSelectedRoute ? " selected-route" : ""}" x1="${a.x}" y1="${a.y}" x2="${b.x}" y2="${b.y}"></line>
            </g>
        `;
    }).join("");

    const lensMetrics = galaxy.systems.map(system => mapLensMetric(
        system,
        presenceBySystem.get(system.systemId) ?? {},
        empire.empireId));
    const maximumLensMetric = Math.max(1, ...lensMetrics.filter((_, index) => composition.systemIds.has(galaxy.systems[index].systemId)));

    const nodes = galaxy.systems.map((system, index) => {
        const presence = presenceBySystem.get(system.systemId) ?? {};
        const ownPresence = Number(presence[empire.empireId] ?? 0);
        const activePresence = Object.values(presence).map(Number).filter(value => value > 0);
        const totalPresence = activePresence.reduce((total, value) => total + value, 0);
        const isContested = activePresence.length > 1;
        const lensMetric = lensMetrics[index];
        const lensIntensity = lensMetric / maximumLensMetric;
        const radius = state.mapLens === "overview"
            ? 4 + Math.min(7, Math.sqrt(ownPresence) * 0.55)
            : 4 + 8 * Math.sqrt(lensIntensity);
        const isSelected = system.systemId === selectedId;
        const isImportant = isSelected || system.systemId === homeId || isContested || system.historicalSignificance > 0;
        const isGateway = Boolean(system.isGateway) || sectorContext.gatewaySystemIds.has(system.systemId);
        const isActiveSector = system.sectorId === state.selectedSectorId;
        const isAdjacentGateway = sectorContext.adjacentGatewaySystemIds.has(system.systemId);
        const isInComposition = composition.systemIds.has(system.systemId);
        const isLocalContext = composition.localContextSystemIds.has(system.systemId);
        const classes = [
            "system",
            system.historicalSignificance > 0 ? "historic" : "",
            system.systemId === homeId ? "home" : "",
            isContested ? "contested" : "",
            isSelected ? "selected" : ""
        ].filter(Boolean).join(" ");
        const label = `${system.systemName}: strategic ${system.strategicValue}, your presence ${formatNumber(ownPresence)}, total visible presence ${formatNumber(totalPresence)}, ${mapLensMetricLabel(system, lensMetric)}`;
        const nodeClasses = [
            "system-node",
            isInComposition ? "is-in-composition" : "",
            isLocalContext ? "is-local-context" : "",
            isImportant ? "is-important" : "",
            isGateway ? "is-gateway" : "",
            isActiveSector ? "is-active-sector" : "",
            isAdjacentGateway ? "is-adjacent-gateway" : ""
        ].filter(Boolean).join(" ");

        return `
            <g class="${nodeClasses}" data-system-id="${system.systemId}" role="button" tabindex="0" aria-label="${escapeHtml(label)}" style="--lens-intensity: ${lensIntensity}">
                <title>${escapeHtml(label)}</title>
                <circle class="system-aura" cx="${system.x}" cy="${system.y}" r="${radius + 11}"></circle>
                ${ownPresence > 0 ? `<circle class="presence" cx="${system.x}" cy="${system.y}" r="${radius + 5}"></circle>` : ""}
                ${isContested ? `<circle class="contested-ring" cx="${system.x}" cy="${system.y}" r="${radius + 10}"></circle>` : ""}
                ${isGateway ? `<circle class="gateway-ring" cx="${system.x}" cy="${system.y}" r="${radius + 7}"></circle>` : ""}
                ${isSelected ? `
                    <circle class="selection-scan" cx="${system.x}" cy="${system.y}" r="${radius + 29}"></circle>
                    <circle class="selection-orbit" cx="${system.x}" cy="${system.y}" r="${radius + 16}"></circle>
                    ${mapSelectionReticle(system.x, system.y, radius + 23)}
                ` : ""}
                <circle class="${classes}" cx="${system.x}" cy="${system.y}" r="${radius}"></circle>
                <circle class="system-core" cx="${system.x}" cy="${system.y}" r="${Math.max(2.8, radius * 0.24)}"></circle>
                <text class="system-label${isSelected ? " selected-label" : ""}" x="${system.x + radius + 4}" y="${system.y + 2.5}">${escapeHtml(system.systemName)}</text>
            </g>
        `;
    }).join("");

    elements.galaxyMap.dataset.mapLens = state.mapLens;
    elements.galaxyMap.dataset.mapRange = currentMapRange();
    elements.galaxyMap.innerHTML = `
        <defs>
            <pattern id="chartGrid" width="50" height="50" patternUnits="userSpaceOnUse">
                <path d="M 50 0 L 0 0 0 50" class="chart-grid-line"></path>
            </pattern>
            <radialGradient id="chartField" cx="50%" cy="42%" r="72%">
                <stop offset="0" stop-color="#18213a"></stop>
                <stop offset="0.48" stop-color="#0b1020"></stop>
                <stop offset="1" stop-color="#050711"></stop>
            </radialGradient>
            <linearGradient id="galacticPlane" x1="0" y1="0" x2="1" y2="1">
                <stop offset="0" stop-color="#5875aa" stop-opacity="0"></stop>
                <stop offset="0.5" stop-color="#6f83b4" stop-opacity="0.12"></stop>
                <stop offset="1" stop-color="#d4a85b" stop-opacity="0"></stop>
            </linearGradient>
        </defs>
        <rect class="chart-field" x="-80" y="-80" width="1160" height="860"></rect>
        <rect class="chart-grid" x="-80" y="-80" width="1160" height="860"></rect>
        <g class="starfield-layer">${renderMapStarfield()}</g>
        <ellipse class="galactic-plane" cx="500" cy="350" rx="610" ry="158" transform="rotate(-11 500 350)"></ellipse>
        <g class="sector-layer">${sectorLayer}</g>
        <g class="route-layer">${lines}</g>
        <g class="system-layer">${nodes}</g>
    `;
    const systemOptions = galaxy.systems
        .slice()
        .sort((left, right) => left.systemName.localeCompare(right.systemName))
        .map(system => `<option value="${escapeHtml(system.systemName)}" label="System"></option>`);
    const sectorOptions = sectors
        .slice()
        .sort((left, right) => left.sortOrder - right.sortOrder || left.sectorName.localeCompare(right.sectorName))
        .map(sector => `<option value="${escapeHtml(mapSectorDisplayName(sector))}" label="Sector"></option>`);
    elements.systemOptions.innerHTML = [...systemOptions, ...sectorOptions].join("");
    if (selectedSystem && document.activeElement !== elements.systemSearch) {
        elements.systemSearch.value = selectedSystem.systemName;
    }
    for (const button of elements.mapLensButtons) {
        button.setAttribute("aria-pressed", String(button.dataset.mapLens === state.mapLens));
    }
    applyMapViewBox();
    renderMapOwnershipStats(galaxy, empire, presenceBySystem);
    renderMapInsight(galaxy, empire, presenceBySystem);
    renderMapNavigator(galaxy, empire);
    renderRecentMapSystems();
    renderMapRecoveryState(galaxy, presenceBySystem);
}

function normaliseGalaxySectors(galaxy) {
    if (!galaxy) {
        return [];
    }

    if (Array.isArray(galaxy.sectors) && galaxy.sectors.length > 0) {
        return galaxy.sectors;
    }

    const groupedSystems = groupSystemsBySector(galaxy.systems);
    return [...groupedSystems.entries()].map(([sectorId, systems], index) => ({
        sectorId,
        sectorName: groupedSystems.size === 1 ? "Known Space" : `Sector Y${index}`,
        centreX: Math.round(systems.reduce((total, system) => total + system.x, 0) / systems.length),
        centreY: Math.round(systems.reduce((total, system) => total + system.y, 0) / systems.length),
        sortOrder: index,
        systemCount: systems.length
    }));
}

function groupSystemsBySector(systems) {
    const groups = new Map();
    for (const system of systems) {
        const sectorId = system.sectorId || "legacy-galaxy";
        const members = groups.get(sectorId) ?? [];
        members.push(system);
        groups.set(sectorId, members);
    }
    return groups;
}

function mapSectorDisplayName(sector) {
    const sortOrder = Number(sector.sortOrder);
    const code = Number.isFinite(sortOrder)
        ? `Y${String(sortOrder).padStart(2, "0")}`
        : "Y??";
    return `${code} · ${sector.sectorName}`;
}

function mapSectorContext(galaxy, activeSectorId) {
    const systems = new Map(galaxy.systems.map(system => [system.systemId, system]));
    const gatewaySystemIds = new Set();
    const adjacentSectorIds = new Set();
    const adjacentGatewaySystemIds = new Set();

    for (const link of galaxy.links) {
        const a = systems.get(link.systemAId);
        const b = systems.get(link.systemBId);
        if (!a || !b || a.sectorId === b.sectorId) {
            continue;
        }

        gatewaySystemIds.add(a.systemId);
        gatewaySystemIds.add(b.systemId);
        if (a.sectorId === activeSectorId) {
            adjacentSectorIds.add(b.sectorId);
            adjacentGatewaySystemIds.add(b.systemId);
        }
        if (b.sectorId === activeSectorId) {
            adjacentSectorIds.add(a.sectorId);
            adjacentGatewaySystemIds.add(a.systemId);
        }
    }

    return { gatewaySystemIds, adjacentSectorIds, adjacentGatewaySystemIds };
}

function mapComposition(galaxy, range = currentMapRange()) {
    const sectors = normaliseGalaxySectors(galaxy);
    const systems = new Map(galaxy.systems.map(system => [system.systemId, system]));
    const selected = systems.get(state.selectedSystemId);
    const activeSectorId = state.selectedSectorId ?? selected?.sectorId ?? sectors[0]?.sectorId;
    const sectorIds = new Set();
    const systemIds = new Set();
    const linkIds = new Set();
    const localContextSystemIds = new Set();
    const includeSystem = system => {
        if (!system) {
            return;
        }
        systemIds.add(system.systemId);
        if (system.sectorId) {
            sectorIds.add(system.sectorId);
        }
    };
    const includeLink = link => linkIds.add(mapLinkKey(link));

    if (range === "galaxy") {
        for (const sector of sectors) {
            sectorIds.add(sector.sectorId);
        }
        for (const link of galaxy.links) {
            const a = systems.get(link.systemAId);
            const b = systems.get(link.systemBId);
            if (a && b && a.sectorId !== b.sectorId) {
                includeLink(link);
            }
        }
        return { range, sectorIds, systemIds, linkIds, localContextSystemIds };
    }

    if (range === "sector") {
        sectorIds.add(activeSectorId);
        for (const system of galaxy.systems) {
            if (system.sectorId === activeSectorId) {
                includeSystem(system);
            }
        }
        for (const link of galaxy.links) {
            const a = systems.get(link.systemAId);
            const b = systems.get(link.systemBId);
            if (!a || !b) {
                continue;
            }
            if (a.sectorId === activeSectorId && b.sectorId === activeSectorId) {
                includeSystem(a);
                includeSystem(b);
                includeLink(link);
            } else if (a.sectorId === activeSectorId || b.sectorId === activeSectorId) {
                includeLink(link);
            }
        }
        return { range, sectorIds, systemIds, linkIds, localContextSystemIds };
    }

    if (selected) {
        includeSystem(selected);
        localContextSystemIds.add(selected.systemId);
    }

    for (const link of galaxy.links) {
        if (link.systemAId !== selected?.systemId && link.systemBId !== selected?.systemId) {
            continue;
        }
        const a = systems.get(link.systemAId);
        const b = systems.get(link.systemBId);
        includeSystem(a);
        includeSystem(b);
        includeLink(link);
    }

    for (const link of galaxy.links) {
        if (systemIds.has(link.systemAId) && systemIds.has(link.systemBId)) {
            includeLink(link);
        }
    }
    for (const systemId of systemIds) {
        localContextSystemIds.add(systemId);
    }
    return { range: "local", sectorIds, systemIds, linkIds, localContextSystemIds };
}

function mapLinkKey(link) {
    return link.systemLinkId ?? [link.systemAId, link.systemBId].sort().join(":");
}

function renderMapSectorLayer(galaxy, sectors, context, composition = mapComposition(galaxy)) {
    const systemsBySector = groupSystemsBySector(galaxy.systems);
    return sectors.map(sector => {
        const members = systemsBySector.get(sector.sectorId) ?? [];
        const path = mapSectorEnvelopePath(members, sector);
        const isActive = sector.sectorId === state.selectedSectorId;
        const isAdjacent = context.adjacentSectorIds.has(sector.sectorId);
        const isInComposition = composition.sectorIds.has(sector.sectorId);
        const classes = [
            "sector-node",
            isInComposition ? "is-in-composition" : "",
            isActive ? "is-active" : "",
            isAdjacent ? "is-adjacent" : ""
        ].filter(Boolean).join(" ");
        const count = Number(sector.systemCount ?? members.length);
        const displayName = mapSectorDisplayName(sector);
        const label = `${displayName}, ${formatCount(count, "system")}`;
        return `
            <g class="${classes}" data-sector-id="${escapeHtml(sector.sectorId)}" role="button" tabindex="0" aria-label="${escapeHtml(label)}">
                <title>${escapeHtml(label)}. Select to enter this sector.</title>
                <path class="sector-hull" d="${path}"></path>
                <circle class="sector-hit" cx="${sector.centreX}" cy="${sector.centreY}" r="30"></circle>
                <circle class="sector-anchor" cx="${sector.centreX}" cy="${sector.centreY}" r="3"></circle>
                <text class="sector-name" x="${sector.centreX}" y="${sector.centreY - 4}">${escapeHtml(displayName)}</text>
                <text class="sector-count" x="${sector.centreX}" y="${sector.centreY + 11}">${formatCount(count, "system")}</text>
            </g>
        `;
    }).join("");
}

function mapSectorEnvelopePath(systems, sector) {
    const points = systems.map(system => ({ x: Number(system.x), y: Number(system.y) }));
    if (points.length < 3) {
        const radius = points.length === 2
            ? Math.max(38, Math.hypot(points[0].x - points[1].x, points[0].y - points[1].y) / 2 + 24)
            : 48;
        const centreX = Number(sector.centreX);
        const centreY = Number(sector.centreY);
        return `M ${centreX - radius} ${centreY} A ${radius} ${radius} 0 1 0 ${centreX + radius} ${centreY} A ${radius} ${radius} 0 1 0 ${centreX - radius} ${centreY} Z`;
    }

    const hull = convexHull(points);
    if (hull.length < 3) {
        const minX = Math.min(...points.map(point => point.x));
        const maxX = Math.max(...points.map(point => point.x));
        const minY = Math.min(...points.map(point => point.y));
        const maxY = Math.max(...points.map(point => point.y));
        const centreX = (minX + maxX) / 2;
        const centreY = (minY + maxY) / 2;
        const radiusX = Math.max(42, (maxX - minX) / 2 + 24);
        const radiusY = Math.max(36, (maxY - minY) / 2 + 24);
        return `M ${centreX - radiusX} ${centreY} A ${radiusX} ${radiusY} 0 1 0 ${centreX + radiusX} ${centreY} A ${radiusX} ${radiusY} 0 1 0 ${centreX - radiusX} ${centreY} Z`;
    }
    const centre = hull.reduce((total, point) => ({ x: total.x + point.x, y: total.y + point.y }), { x: 0, y: 0 });
    centre.x /= hull.length;
    centre.y /= hull.length;
    const expanded = hull.map(point => {
        const dx = point.x - centre.x;
        const dy = point.y - centre.y;
        const length = Math.max(1, Math.hypot(dx, dy));
        return { x: point.x + dx / length * 22, y: point.y + dy / length * 22 };
    });
    const midpoint = (a, b) => ({ x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 });
    const start = midpoint(expanded.at(-1), expanded[0]);
    const commands = expanded.map((point, index) => {
        const next = expanded[(index + 1) % expanded.length];
        const end = midpoint(point, next);
        return `Q ${point.x.toFixed(1)} ${point.y.toFixed(1)} ${end.x.toFixed(1)} ${end.y.toFixed(1)}`;
    });
    return `M ${start.x.toFixed(1)} ${start.y.toFixed(1)} ${commands.join(" ")} Z`;
}

function convexHull(points) {
    const sorted = points
        .slice()
        .sort((left, right) => left.x - right.x || left.y - right.y);
    const cross = (origin, a, b) => (a.x - origin.x) * (b.y - origin.y) - (a.y - origin.y) * (b.x - origin.x);
    const buildHalf = source => {
        const half = [];
        for (const point of source) {
            while (half.length >= 2 && cross(half.at(-2), half.at(-1), point) <= 0) {
                half.pop();
            }
            half.push(point);
        }
        return half;
    };
    const lower = buildHalf(sorted);
    const upper = buildHalf(sorted.slice().reverse());
    return [...lower.slice(0, -1), ...upper.slice(0, -1)];
}

function renderMapStarfield() {
    return Array.from({ length: 112 }, (_, index) => {
        const x = (index * 83 + (index % 7) * 29) % mapBounds.width;
        const y = (index * 47 + (index % 11) * 41) % mapBounds.height;
        const radius = index % 13 === 0 ? 1.65 : index % 5 === 0 ? 1.05 : 0.62;
        const depth = index % 3;
        return `<circle class="chart-star chart-star-${depth}" cx="${x}" cy="${y}" r="${radius}"></circle>`;
    }).join("");
}

function mapSelectionReticle(x, y, radius) {
    const arm = 10;
    return `
        <g class="selection-reticle" transform="translate(${x} ${y})">
            <path d="M ${-radius} ${-radius + arm} V ${-radius} H ${-radius + arm}"></path>
            <path d="M ${radius - arm} ${-radius} H ${radius} V ${-radius + arm}"></path>
            <path d="M ${radius} ${radius - arm} V ${radius} H ${radius - arm}"></path>
            <path d="M ${-radius + arm} ${radius} H ${-radius} V ${radius - arm}"></path>
        </g>
    `;
}

function selectSystem(systemId, { focusMap = false, restoreMapFocus = false } = {}) {
    const system = state.galaxy?.systems.find(candidate => candidate.systemId === systemId);
    if (!system) {
        return;
    }

    state.selectedSystemId = systemId;
    state.selectedSectorId = system.sectorId ?? state.selectedSectorId;
    rememberMapSystem(systemId);
    if (focusMap) {
        focusMapOnSystem(systemId);
    }
    renderSystemDetails();
    renderGalaxy(state.galaxy, state.empire);
    if (restoreMapFocus) {
        focusRenderedMapNode(".system-node", "systemId", systemId);
    }
    syncTutorialDisplay();
}

function setMapLens(lens) {
    if (!Object.hasOwn(mapLensLabels, lens) || lens === state.mapLens) {
        return;
    }

    state.mapLens = lens;
    renderGalaxy(state.galaxy, state.empire);
}

function mapLensMetric(system, presence, empireId) {
    switch (state.mapLens) {
        case "presence":
            return Object.values(presence).reduce((total, value) => total + Number(value), 0);
        case "strategy":
            return Number(system.strategicValue);
        case "output":
            return systemOutput(system);
        case "history":
            return Number(system.historicalSignificance);
        default:
            return Number(presence[empireId] ?? 0);
    }
}

function mapLensMetricLabel(system, lensMetric) {
    switch (state.mapLens) {
        case "presence":
            return `visible presence ${formatNumber(lensMetric)}`;
        case "strategy":
            return `strategic value ${formatNumber(lensMetric)}`;
        case "output":
            return `combined output ${formatNumber(lensMetric)}`;
        case "history":
            return `historical signal ${formatNumber(lensMetric)}`;
        default:
            return `combined output ${formatNumber(systemOutput(system))}`;
    }
}

function systemOutput(system) {
    return Number(system.industryOutput) + Number(system.researchOutput) + Number(system.populationOutput);
}

function renderMapInsight(galaxy, empire, presenceBySystem) {
    elements.mapInsightLabel.textContent = mapLensLabels[state.mapLens];
    const selected = galaxy.systems.find(system => system.systemId === state.selectedSystemId);
    const contested = galaxy.systems.filter(system => {
        const presence = presenceBySystem.get(system.systemId) ?? {};
        return Object.values(presence).filter(value => Number(value) > 0).length > 1;
    });

    if (state.mapLens === "presence") {
        const held = galaxy.systems.filter(system => Number((presenceBySystem.get(system.systemId) ?? {})[empire.empireId] ?? 0) > 0);
        const frontier = contested.length === 0
            ? "No visible system is currently contested."
            : `${formatCount(contested.length, "visible flashpoint")} led by ${contested[0].systemName}.`;
        elements.mapInsight.textContent = `${empire.empireName} projects presence into ${formatCount(held.length, "system")}. ${frontier}`;
        return;
    }

    if (state.mapLens === "strategy") {
        const highest = galaxy.systems.slice().sort((left, right) => right.strategicValue - left.strategicValue || left.systemName.localeCompare(right.systemName))[0];
        elements.mapInsight.textContent = `${highest.systemName} is the strongest visible strategic anchor at ${formatNumber(highest.strategicValue)}.`;
        return;
    }

    if (state.mapLens === "output") {
        const richest = galaxy.systems.slice().sort((left, right) => systemOutput(right) - systemOutput(left) || left.systemName.localeCompare(right.systemName))[0];
        elements.mapInsight.textContent = `${richest.systemName} leads visible capacity with ${formatNumber(systemOutput(richest))} combined output.`;
        return;
    }

    if (state.mapLens === "history") {
        const historic = galaxy.systems.slice().sort((left, right) => right.historicalSignificance - left.historicalSignificance || left.systemName.localeCompare(right.systemName))[0];
        elements.mapInsight.textContent = historic.historicalSignificance > 0
            ? `${historic.systemName} carries the strongest historical signal at ${formatNumber(historic.historicalSignificance)}.`
            : "No system has accumulated a lasting historical signal yet.";
        return;
    }

    if (!selected) {
        elements.mapInsight.textContent = "Select a system to expose its immediate strategic context.";
        return;
    }

    const routeCount = galaxy.links.filter(link => link.systemAId === selected.systemId || link.systemBId === selected.systemId).length;
    const localFleetCount = state.fleets.filter(item => item.fleet.currentSystemId === selected.systemId && item.fleet.status === "active" && item.fleet.shipCount > 0).length;
    const sector = normaliseGalaxySectors(galaxy).find(candidate => candidate.sectorId === selected.sectorId);
    const gatewaySignal = selected.isGateway ? " It controls an inter-sector gate." : "";
    elements.mapInsight.textContent = `${selected.systemName} in ${sector ? mapSectorDisplayName(sector) : "uncharted space"} opens onto ${formatCount(routeCount, "route")} with ${formatCount(localFleetCount, "friendly fleet")} on station.${gatewaySignal}`;
}

function applyMapPreset(preset) {
    setMapRange(preset);
}

function setMapRange(range) {
    const preset = range;
    if (!Object.hasOwn(mapPresetWidths, preset)) {
        return;
    }

    state.mapPreset = preset;
    if (preset === "galaxy") {
        state.mapViewBox = { ...mapBounds };
    } else if (preset === "sector") {
        const sectorId = state.selectedSectorId
            ?? state.galaxy?.systems.find(system => system.systemId === state.selectedSystemId)?.sectorId;
        setMapViewAroundSector(sectorId);
    } else {
        const targetId = state.selectedSystemId ?? state.empire?.homeSystem.systemId;
        setMapViewAroundSystem(targetId, mapPresetWidths[preset]);
    }
    constrainMapViewBox();
    if (state.galaxy && state.empire) {
        renderGalaxy(state.galaxy, state.empire);
    } else {
        applyMapViewBox();
    }
}

function focusMapOnSystem(systemId) {
    const system = state.galaxy?.systems.find(candidate => candidate.systemId === systemId);
    if (!system) {
        return;
    }

    state.selectedSectorId = system.sectorId ?? state.selectedSectorId;
    state.mapPreset = "sector";
    setMapViewAroundSector(state.selectedSectorId);
}

function focusMapOnSector(sectorId, { recenter = true, restoreMapFocus = false } = {}) {
    if (!normaliseGalaxySectors(state.galaxy).some(sector => sector.sectorId === sectorId)) {
        return;
    }

    selectSectorRepresentative(sectorId);
    if (recenter) {
        state.mapPreset = "sector";
        setMapViewAroundSector(sectorId);
    }
    renderSystemDetails();
    renderGalaxy(state.galaxy, state.empire);
    if (restoreMapFocus) {
        focusRenderedMapNode(".sector-node", "sectorId", sectorId);
    }
}

function selectSectorRepresentative(sectorId) {
    state.selectedSectorId = sectorId;
    const selected = state.galaxy.systems.find(system => system.systemId === state.selectedSystemId);
    if (selected?.sectorId !== sectorId) {
        const representative = state.galaxy.systems
            .filter(system => system.sectorId === sectorId)
            .sort((left, right) => Number(right.isGateway) - Number(left.isGateway)
                || right.strategicValue - left.strategicValue
                || left.systemName.localeCompare(right.systemName))[0];
        if (representative) {
            state.selectedSystemId = representative.systemId;
            rememberMapSystem(representative.systemId);
        }
    }
}

function focusRenderedMapNode(selector, dataName, value) {
    const node = [...elements.galaxyMap.querySelectorAll(selector)]
        .find(candidate => candidate.dataset[dataName] === value && candidate.getAttribute("aria-hidden") !== "true");
    (node ?? elements.galaxyMap).focus({ preventScroll: true });
}

function setMapViewAroundSector(sectorId) {
    const sector = normaliseGalaxySectors(state.galaxy).find(candidate => candidate.sectorId === sectorId);
    const members = state.galaxy?.systems.filter(system => (system.sectorId || "legacy-galaxy") === sectorId) ?? [];
    if (!sector || members.length === 0) {
        const targetId = state.selectedSystemId ?? state.empire?.homeSystem.systemId;
        setMapViewAroundSystem(targetId, mapPresetWidths.sector);
        return;
    }

    const minX = Math.min(...members.map(system => system.x));
    const maxX = Math.max(...members.map(system => system.x));
    const minY = Math.min(...members.map(system => system.y));
    const maxY = Math.max(...members.map(system => system.y));
    const horizontalSpan = maxX - minX + 100;
    const verticalSpan = (maxY - minY + 100) * mapBounds.width / mapBounds.height;
    const width = Math.min(520, Math.max(mapPresetWidths.sector, horizontalSpan, verticalSpan));
    const height = width * mapBounds.height / mapBounds.width;
    state.mapViewBox = {
        x: Number(sector.centreX) - width / 2,
        y: Number(sector.centreY) - height / 2,
        width,
        height
    };
    constrainMapViewBox();
}

function setMapViewAroundSystem(systemId, width) {
    const system = state.galaxy?.systems.find(item => item.systemId === systemId);
    if (!system) {
        return;
    }

    const height = width * mapBounds.height / mapBounds.width;
    state.mapViewBox = {
        x: system.x - width / 2,
        y: system.y - height / 2,
        width,
        height
    };
    constrainMapViewBox();
}

function recoverMapToSystem(systemId) {
    if (!systemId || !state.galaxy?.systems.some(system => system.systemId === systemId)) {
        return;
    }

    if (state.selectedSystemId !== systemId) {
        selectSystem(systemId);
    } else {
        rememberMapSystem(systemId);
        renderRecentMapSystems();
    }
    state.mapPreset = "sector";
    const system = state.galaxy.systems.find(candidate => candidate.systemId === systemId);
    state.selectedSectorId = system?.sectorId ?? state.selectedSectorId;
    setMapViewAroundSector(state.selectedSectorId);
    applyMapViewBox();
    elements.galaxyMap.focus({ preventScroll: true });
}

function recoverMapToFrontier() {
    const frontier = visibleFlashpoints(state.galaxy)[0];
    if (frontier) {
        recoverMapToSystem(frontier.systemId);
    }
}

function visibleFlashpoints(galaxy, presenceBySystem = null) {
    if (!galaxy) {
        return [];
    }

    const presence = presenceBySystem ?? new Map(galaxy.presence.map(item => [item.systemId, item.effectivePresence]));
    return galaxy.systems
        .filter(system => Object.values(presence.get(system.systemId) ?? {}).filter(value => Number(value) > 0).length > 1)
        .sort((left, right) => {
            const leftPresence = Object.values(presence.get(left.systemId) ?? {}).reduce((total, value) => total + Number(value), 0);
            const rightPresence = Object.values(presence.get(right.systemId) ?? {}).reduce((total, value) => total + Number(value), 0);
            return rightPresence - leftPresence
                || right.strategicValue - left.strategicValue
                || left.systemName.localeCompare(right.systemName);
        });
}

function renderMapRecoveryState(galaxy, presenceBySystem) {
    const frontier = visibleFlashpoints(galaxy, presenceBySystem)[0];
    elements.mapFocusHome.disabled = !state.empire?.homeSystem.systemId;
    elements.mapFocusSelected.disabled = !state.selectedSystemId;
    elements.mapFocusFrontier.disabled = !frontier;
    elements.mapFocusFrontier.title = frontier
        ? `Recover ${frontier.systemName}, the strongest visible flashpoint`
        : "No visible flashpoints";
}

function rememberMapSystem(systemId) {
    if (!systemId) {
        return;
    }

    state.mapRecentSystemIds = [
        systemId,
        ...state.mapRecentSystemIds.filter(candidate => candidate !== systemId)
    ].slice(0, 5);
}

function renderRecentMapSystems() {
    const systems = new Map((state.galaxy?.systems ?? []).map(system => [system.systemId, system]));
    elements.mapRecentSystems.innerHTML = state.mapRecentSystemIds
        .map(systemId => systems.get(systemId))
        .filter(Boolean)
        .map(system => `
            <button type="button" data-recent-system="${escapeHtml(system.systemId)}"${system.systemId === state.selectedSystemId ? " aria-current=\"true\"" : ""}>
                ${escapeHtml(system.systemName)}
            </button>
        `)
        .join("");
}

function renderMapNavigator(galaxy, empire) {
    const systems = new Map(galaxy.systems.map(system => [system.systemId, system]));
    const sectors = normaliseGalaxySectors(galaxy);
    const routes = galaxy.links.filter(link => {
        const a = systems.get(link.systemAId);
        const b = systems.get(link.systemBId);
        return a && b && a.sectorId !== b.sectorId;
    }).map(link => {
        const a = systems.get(link.systemAId);
        const b = systems.get(link.systemBId);
        return `<line x1="${a.x}" y1="${a.y}" x2="${b.x}" y2="${b.y}"></line>`;
    }).join("");
    const sectorNodes = sectors.map(sector => {
        const systemCount = Number(sector.systemCount
            ?? galaxy.systems.filter(system => system.sectorId === sector.sectorId).length);
        const classes = [
            "navigator-sector",
            sector.sectorId === empire.homeSystem.sectorId ? "is-home" : "",
            sector.sectorId === state.selectedSectorId ? "is-selected" : ""
        ].filter(Boolean).join(" ");
        return `<circle class="${classes}" cx="${sector.centreX}" cy="${sector.centreY}" r="22"><title>${escapeHtml(mapSectorDisplayName(sector))}, ${formatCount(systemCount, "system")}</title></circle>`;
    }).join("");
    const selected = systems.get(state.selectedSystemId);
    const markers = [empire.homeSystem, selected]
        .filter(Boolean)
        .filter((system, index, items) => items.findIndex(candidate => candidate.systemId === system.systemId) === index)
        .map(system => `<circle class="navigator-system${system.systemId === empire.homeSystem.systemId ? " is-home" : ""}${system.systemId === state.selectedSystemId ? " is-selected" : ""}" cx="${system.x}" cy="${system.y}" r="9"><title>${escapeHtml(system.systemName)}</title></circle>`)
        .join("");

    elements.mapNavigator.innerHTML = `
        <rect class="navigator-field" x="0" y="0" width="1000" height="700"></rect>
        <g class="navigator-routes">${routes}</g>
        <g class="navigator-sectors">${sectorNodes}</g>
        <g class="navigator-systems">${markers}</g>
        <rect id="mapNavigatorViewport" class="navigator-viewport" rx="8"></rect>
    `;
    syncNavigatorViewport();
}

function moveMapFromNavigator(event) {
    const rect = elements.mapNavigator.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) {
        return;
    }

    const centreX = mapBounds.x + (event.clientX - rect.left) / rect.width * mapBounds.width;
    const centreY = mapBounds.y + (event.clientY - rect.top) / rect.height * mapBounds.height;
    const sector = nearestMapSector(centreX, centreY);
    if (sector) {
        focusMapOnSector(sector.sectorId);
    }
}

function syncMapSectorContextToCamera() {
    if (currentMapRange() === "galaxy") {
        return false;
    }

    const centreX = state.mapViewBox.x + state.mapViewBox.width / 2;
    const centreY = state.mapViewBox.y + state.mapViewBox.height / 2;
    const sector = nearestMapSector(centreX, centreY);
    if (!sector || sector.sectorId === state.selectedSectorId) {
        return false;
    }

    focusMapOnSector(sector.sectorId, { recenter: false });
    return true;
}

function nearestMapSector(x, y) {
    return normaliseGalaxySectors(state.galaxy)
        .slice()
        .sort((left, right) => {
            const leftDistance = Math.hypot(Number(left.centreX) - x, Number(left.centreY) - y);
            const rightDistance = Math.hypot(Number(right.centreX) - x, Number(right.centreY) - y);
            return leftDistance - rightDistance || left.sortOrder - right.sortOrder;
        })[0] ?? null;
}

function directionalMapSector(sectorId, key) {
    const directions = {
        ArrowLeft: { x: -1, y: 0 },
        ArrowUp: { x: 0, y: -1 },
        ArrowRight: { x: 1, y: 0 },
        ArrowDown: { x: 0, y: 1 }
    };
    const direction = directions[key];
    const sectors = normaliseGalaxySectors(state.galaxy);
    const current = sectors.find(sector => sector.sectorId === sectorId);
    if (!direction || !current) {
        return null;
    }

    return sectors
        .filter(sector => sector.sectorId !== current.sectorId)
        .map(sector => {
            const deltaX = Number(sector.centreX) - Number(current.centreX);
            const deltaY = Number(sector.centreY) - Number(current.centreY);
            const distance = Math.hypot(deltaX, deltaY);
            const projection = deltaX * direction.x + deltaY * direction.y;
            return {
                sector,
                projection,
                alignment: distance > 0 ? projection / distance : -1,
                distance
            };
        })
        .filter(candidate => candidate.projection > 0)
        .sort((left, right) => right.alignment - left.alignment
            || left.distance - right.distance
            || left.sector.sortOrder - right.sector.sortOrder)[0]?.sector ?? null;
}

function constrainMapViewBox() {
    state.mapViewBox.width = Math.min(mapBounds.width, Math.max(140, state.mapViewBox.width));
    state.mapViewBox.height = state.mapViewBox.width * mapBounds.height / mapBounds.width;
    state.mapViewBox.x = Math.min(mapBounds.x + mapBounds.width - state.mapViewBox.width, Math.max(mapBounds.x, state.mapViewBox.x));
    state.mapViewBox.y = Math.min(mapBounds.y + mapBounds.height - state.mapViewBox.height, Math.max(mapBounds.y, state.mapViewBox.y));
}

function applyMapViewBox() {
    const viewBox = state.mapViewBox;
    elements.galaxyMap.setAttribute("viewBox", `${viewBox.x} ${viewBox.y} ${viewBox.width} ${viewBox.height}`);
    elements.galaxyMap.dataset.mapRange = currentMapRange();
    syncMapSemanticFocus();
    for (const button of elements.mapPresetButtons) {
        button.setAttribute("aria-pressed", String(button.dataset.mapPreset === state.mapPreset));
    }
    syncNavigatorViewport();
}

function syncMapSemanticFocus() {
    const range = elements.galaxyMap.dataset.mapRange;
    for (const node of elements.galaxyMap.querySelectorAll(".system-node")) {
        const isAvailable = node.classList.contains("is-in-composition")
            && range !== "galaxy"
            && (range !== "local" || node.classList.contains("is-local-context"));
        node.setAttribute("tabindex", isAvailable ? "0" : "-1");
        node.setAttribute("aria-hidden", String(!isAvailable));
    }

    for (const node of elements.galaxyMap.querySelectorAll(".sector-node")) {
        const isAvailable = node.classList.contains("is-in-composition")
            && (range !== "local" || node.classList.contains("is-active"));
        node.setAttribute("tabindex", isAvailable ? "0" : "-1");
        node.setAttribute("aria-hidden", String(!isAvailable));
    }
}

function currentMapRange() {
    return state.mapPreset ?? "galaxy";
}

function syncNavigatorViewport() {
    const viewport = elements.mapNavigator.querySelector("#mapNavigatorViewport");
    if (!viewport) {
        return;
    }

    viewport.setAttribute("x", state.mapViewBox.x);
    viewport.setAttribute("y", state.mapViewBox.y);
    viewport.setAttribute("width", state.mapViewBox.width);
    viewport.setAttribute("height", state.mapViewBox.height);
}

function setMapMaximised(maximised) {
    state.mapMaximised = Boolean(maximised);
    document.body.classList.toggle("map-maximised", state.mapMaximised);
    elements.galaxyWorkspace.classList.toggle("is-maximised", state.mapMaximised);
    elements.mapMaximise.setAttribute("aria-pressed", String(state.mapMaximised));
    elements.mapMaximise.title = state.mapMaximised ? "Restore galaxy map" : "Maximise galaxy map";
    elements.mapMaximise.querySelector(".map-maximise-label").textContent = state.mapMaximised ? "Restore" : "Maximise";
}

async function selectFleet(fleetId) {
    state.selectedFleetId = fleetId;
    try {
        state.fleetDetail = await getJson(`/fleets/${fleetId}`);
        renderFleets(state.fleets);
        renderFleetDetails();
        renderOrders();
        renderOrderHistory();
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
        .map(link => {
            const destination = systems.get(link.systemAId === systemId ? link.systemBId : link.systemAId);
            return destination
                ? { ...destination, routeDistance: link.distance, routeTravelTicks: link.travelTicks }
                : null;
        })
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
        const error = new Error(payload.message ?? "Request failed.");
        error.code = payload.code ?? "requestFailed";
        error.details = payload.details ?? null;
        error.traceId = payload.traceId ?? null;
        throw error;
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

function rebalancePriorityDraft(activeKey, requestedValue) {
    if (!activePriorityKeys.includes(activeKey)) {
        return;
    }

    const activeValue = Math.max(0, Math.min(100, requestedValue));
    const pointDelta = activeValue - state.priorityDraft[activeKey];
    const otherKeys = activePriorityKeys.filter(key => key !== activeKey);
    state.priorityDraft[activeKey] = activeValue;

    for (let point = 0; point < Math.abs(pointDelta); point += 1) {
        const transferKey = otherKeys.reduce((selectedKey, candidateKey) => {
            const selectedValue = state.priorityDraft[selectedKey];
            const candidateValue = state.priorityDraft[candidateKey];
            const candidateIsBetter = pointDelta > 0
                ? candidateValue > selectedValue
                : candidateValue < selectedValue;
            return candidateIsBetter ? candidateKey : selectedKey;
        });

        state.priorityDraft[transferKey] -= Math.sign(pointDelta);
    }
}

function renderPriorityControls() {
    const total = priorityKeys.reduce((sum, key) => sum + state.priorityDraft[key], 0);
    const isDirty = Boolean(state.empire) && priorityKeys.some(key => state.priorityDraft[key] !== parseWeight(state.empire.priorities[key]));

    elements.priorityInputs.forEach(input => {
        const key = input.dataset.priorityKey;
        const value = state.priorityDraft[key];
        const savedValue = state.empire ? parseWeight(state.empire.priorities[key]) : value;
        const isChanged = value !== savedValue;
        const isInactive = inactivePriorityKeys.includes(key);
        const sliderShell = input.closest(".priority-slider-shell");
        input.value = value;
        input.disabled = state.prioritySaving || isInactive;
        input.setAttribute("aria-valuetext", `${value} points; linked total 100`);
        sliderShell.style.setProperty("--priority-percent", `${value}%`);
        sliderShell.style.setProperty("--saved-percent", `${savedValue}%`);
        sliderShell.classList.toggle("has-saved-marker", isChanged);
        document.querySelector(`#${key}Value`).textContent = value.toLocaleString();
        const savedLabel = document.querySelector(`#${key}Saved`);
        savedLabel.textContent = `Saved ${savedValue.toLocaleString()}`;
        savedLabel.classList.toggle("is-visible", isChanged);
    });

    elements.priorityDraftStatus.textContent = state.prioritySaving ? "Saving" : isDirty ? "Unsaved" : "Saved";
    elements.priorityDraftStatus.hidden = !state.prioritySaving && !isDirty;
    elements.prioritySection.classList.toggle("has-unsaved-changes", Boolean(isDirty));
    elements.priorityForm.setAttribute("aria-busy", state.prioritySaving.toString());
    elements.prioritySaveButton.textContent = state.prioritySaving ? "Saving…" : "Save priorities";
    elements.prioritySaveButton.disabled = !isDirty || total !== 100 || state.prioritySaving;
    elements.priorityResetButton.disabled = !isDirty || state.prioritySaving;
}

function normalisePriorityAllocation(priorities) {
    const militaryWeight = parseWeight(priorities.militaryWeight);
    const expansionWeight = parseWeight(priorities.expansionWeight);
    const activeTotal = militaryWeight + expansionWeight;
    const normalisedMilitary = activeTotal === 0
        ? 50
        : Math.max(0, Math.min(100, Math.round(militaryWeight * 100 / activeTotal)));

    return {
        industryWeight: 0,
        researchWeight: 0,
        militaryWeight: normalisedMilitary,
        expansionWeight: 100 - normalisedMilitary
    };
}

function pulsePriorityConsole() {
    elements.prioritySection.classList.add("is-adjusting");
    window.clearTimeout(priorityActivityTimeout);
    priorityActivityTimeout = window.setTimeout(() => {
        elements.prioritySection.classList.remove("is-adjusting");
    }, 650);
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
    const resourceKey = label.toLowerCase();
    return `
        <div class="resource-card resource-card-${escapeHtml(resourceKey)}">
            <dt>${escapeHtml(label)}</dt>
            <dd>
                <strong>${formatNumber(numeric)}</strong>
                <span class="resource-delta">Last tick +${formatNumber(generatedNumeric)} / -${formatNumber(spentNumeric)}</span>
                <span class="resource-meter"><i style="width: ${width}%"></i></span>
            </dd>
        </div>
    `;
}

function renderMapOwnershipStats(galaxy, empire, presenceBySystem = null) {
    const presence = presenceBySystem ?? new Map(galaxy.presence.map(item => [item.systemId, item.effectivePresence]));
    const reachedSystems = galaxy.systems.filter(system =>
        Number((presence.get(system.systemId) ?? {})[empire.empireId] ?? 0) > 0);
    const reachedSectors = new Set(reachedSystems.map(system => system.sectorId).filter(Boolean));
    const activeFleets = state.fleets.filter(item =>
        item.fleet.empireId === empire.empireId
        && item.fleet.status === "active"
        && item.fleet.shipCount > 0).length;
    const flashpoints = galaxy.systems.filter(system =>
        Object.values(presence.get(system.systemId) ?? {}).filter(value => Number(value) > 0).length > 1).length;
    elements.mapOwnershipStats.innerHTML = `
        ${ownershipStat("Reach", reachedSectors.size, `of ${normaliseGalaxySectors(galaxy).length} sectors`)}
        ${ownershipStat("Presence", reachedSystems.length, "systems")}
        ${ownershipStat("Forces", activeFleets, "active fleets")}
        ${ownershipStat("Pressure", flashpoints, "flashpoints")}
    `;
}

function ownershipStat(label, value, detail) {
    return `
        <span class="ownership-stat">
            <em>${escapeHtml(label)}</em>
            <strong>${formatNumber(value)}</strong>
            <small>${escapeHtml(detail)}</small>
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
