const state = {
    playerId: null,
    username: null,
    role: null,
    canAdvanceTurn: false,
    gamesHome: null,
    games: [],
    gameId: null,
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
    tutorialJourney: null,
    turnResolution: null,
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
    eventSort: "resolution",
    mapLens: "overview",
    mapPreset: "galaxy",
    mapMaximised: false,
    priorityDraft: null,
    prioritySaving: false
};

const viewIds = ["command", "galaxy", "fleets", "history"];
const antiforgeryEndpoint = "/auth/antiforgery";
const antiforgeryHeaderName = "X-Cycles-Antiforgery";
const antiforgeryFormFieldName = "__RequestVerificationToken";
const antiforgeryErrorCode = "antiforgeryFailed";
const gameIdPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
let antiforgeryRequestToken = null;
let antiforgeryTokenPromise = null;
let antiforgeryReady = false;
let acceptedLocationHash = window.location.hash;
const gameApi = createGameApi();
const cycleTurnLimit = 150;
const priorityKeys = ["industryWeight", "researchWeight", "militaryWeight", "expansionWeight"];
const inactivePriorityKeys = ["industryWeight", "researchWeight"];
const activePriorityKeys = ["militaryWeight", "expansionWeight"];
const mapBounds = Object.freeze({ x: 0, y: 0, width: 1586, height: 992 });
const atlasBounds = Object.freeze({ x: -407, y: 0, width: 2400, height: 992 });
const mapRanges = new Set(["galaxy", "sector", "local"]);
const standardGalaxyAtlas = Object.freeze({
    galaxyAsset: "/assets/galaxy/galaxy-overview.webp?v=20260718-webp-1",
    galaxyRoutes: [
        ["Warden Line", "Hollow Crown", "M 515 200 C 585 210 625 210 700 200"],
        ["Warden Line", "Cinder March", "M 320 300 C 315 335 315 355 320 385"],
        ["Hollow Crown", "Lacuna Verge", "M 1040 200 C 1100 210 1130 210 1185 200"],
        ["Hollow Crown", "Aster Reach", "M 960 270 C 1000 310 1020 350 1040 390"],
        ["Lacuna Verge", "Aster Reach", "M 1330 330 C 1330 380 1300 430 1260 455"],
        ["Cinder March", "Umbral Marches", "M 270 585 C 275 650 265 680 265 710"],
        ["Cinder March", "Red Lattice", "M 405 485 C 520 500 600 580 650 665"],
        ["Umbral Marches", "Red Lattice", "M 390 795 C 440 800 470 800 515 795"],
        ["Red Lattice", "Aster Reach", "M 810 710 C 900 610 960 560 1020 540"],
        ["Red Lattice", "Orison Fold", "M 850 800 C 950 825 1050 820 1120 790"],
        ["Aster Reach", "Orison Fold", "M 1170 575 C 1170 650 1190 690 1220 710"]
    ],
    sectors: Object.freeze({
        "Aster Reach": {
            asset: "/assets/galaxy/sector-aster-reach.webp?v=20260718-webp-1",
            galaxy: [1115, 472],
            galaxyContour: "M 1014 409 C 1031 376 1073 360 1115 365 C 1156 350 1210 369 1242 397 C 1284 419 1315 453 1310 492 C 1321 530 1290 566 1250 582 C 1212 607 1162 598 1124 586 C 1082 600 1037 582 1016 551 C 991 526 987 486 1004 458 C 995 439 1000 422 1014 409 Z",
            systems: {
                "Treaty Gate": [292, 300], "Aster Vale": [782, 221], "Nadir Crossing": [1217, 361], "Pale Harbour": [910, 480],
                "Yanaka's Reach": [1220, 744], "Pseudopolis": [619, 716], "Brightfall": [274, 736], "Dawnward": [423, 524]
            },
            routes: [
                ["Treaty Gate", "Aster Vale", [520, 230]], ["Aster Vale", "Nadir Crossing", [1000, 235]],
                ["Nadir Crossing", "Pale Harbour", [1090, 420]], ["Pale Harbour", "Yanaka's Reach", [1040, 620]],
                ["Yanaka's Reach", "Pseudopolis", [930, 830]], ["Pseudopolis", "Brightfall", [450, 755]],
                ["Brightfall", "Dawnward", [330, 650]], ["Dawnward", "Treaty Gate", [335, 410]],
                ["Aster Vale", "Pale Harbour", [820, 350]], ["Pale Harbour", "Pseudopolis", [740, 590]]
            ]
        },
        "Cinder March": {
            asset: "/assets/galaxy/sector-cinder-march.webp?v=20260718-webp-1",
            galaxy: [268, 468],
            galaxyContour: "M 101 424 C 129 389 172 373 216 377 C 260 358 321 373 354 402 C 397 422 422 456 417 492 C 427 531 397 567 357 582 C 320 608 268 604 232 590 C 190 603 139 584 113 551 C 78 527 69 486 86 454 C 78 443 85 432 101 424 Z",
            systems: {
                "Cinderhome": [287, 467], "Ebon Strait": [458, 204], "Glass Meridian": [687, 223], "Keystone": [607, 364],
                "Ashen Gate": [1186, 208], "Cinder Relay": [938, 518], "Pyre Anchorage": [1235, 615], "Ember Watch": [895, 763]
            },
            routes: [
                ["Cinderhome", "Ebon Strait", [350, 300]], ["Ebon Strait", "Glass Meridian", [575, 190]],
                ["Glass Meridian", "Keystone", [700, 300]], ["Keystone", "Cinderhome", [450, 350]],
                ["Ashen Gate", "Cinder Relay", [1160, 390]], ["Cinder Relay", "Pyre Anchorage", [1110, 530]],
                ["Pyre Anchorage", "Ember Watch", [1110, 760]], ["Ember Watch", "Ashen Gate", [1120, 450]],
                ["Ebon Strait", "Pyre Anchorage", [850, 270]], ["Keystone", "Ashen Gate", [900, 320]]
            ]
        },
        "Hollow Crown": {
            asset: "/assets/galaxy/sector-hollow-crown.webp?v=20260718-webp-1",
            galaxy: [808, 178],
            galaxyContour: "M 698 137 C 721 101 761 79 806 79 C 851 68 911 80 949 104 C 995 119 1037 154 1045 190 C 1061 225 1034 264 995 280 C 958 308 905 305 864 294 C 823 306 770 299 735 274 C 694 258 672 225 681 193 C 669 171 679 151 698 137 Z",
            systems: {
                "Hollow Crown": [303, 223], "Juniper Rift": [518, 274], "Hollow Lantern": [719, 307], "Crown Meridian": [943, 275],
                "Hollow Bastion": [1305, 372], "Vigil Cairn": [1125, 677], "Glass Refuge": [767, 832], "Silent Array": [400, 749]
            },
            routes: [
                ["Hollow Crown", "Juniper Rift", "M 303 223 C 358 226 431 274 518 274"],
                ["Juniper Rift", "Hollow Lantern", "M 518 274 C 577 273 651 310 719 307"],
                ["Hollow Lantern", "Crown Meridian", "M 719 307 C 792 273 871 261 943 275"],
                ["Crown Meridian", "Hollow Bastion", "M 943 275 C 1078 260 1195 307 1305 372"],
                ["Hollow Bastion", "Vigil Cairn", "M 1305 372 C 1330 482 1268 620 1125 677"],
                ["Vigil Cairn", "Glass Refuge", "M 1125 677 C 1010 744 892 824 767 832"],
                ["Glass Refuge", "Silent Array", "M 767 832 C 641 847 521 771 400 749"],
                ["Silent Array", "Hollow Crown", "M 400 749 C 263 641 220 408 303 223"],
                ["Juniper Rift", "Glass Refuge", "M 518 274 C 543 462 648 704 767 832"],
                ["Crown Meridian", "Vigil Cairn", "M 943 275 C 982 421 1119 519 1125 677"]
            ]
        },
        "Lacuna Verge": {
            asset: "/assets/galaxy/sector-lacuna-verge.webp?v=20260718-webp-1",
            galaxy: [1300, 182],
            galaxyContour: "M 1208 105 C 1230 66 1274 47 1317 51 C 1355 37 1410 54 1439 82 C 1475 99 1498 131 1493 164 C 1512 193 1497 234 1465 255 C 1441 292 1393 310 1352 302 C 1314 322 1262 309 1234 280 C 1196 262 1177 226 1189 194 C 1176 161 1183 130 1208 105 Z",
            systems: {
                "Lacuna": [357, 190], "Mournstar": [551, 277], "Lacuna Shoal": [795, 219], "Penumbral Span": [1005, 278],
                "Mourn Relay": [1234, 778], "Deep Vault": [997, 699], "Lacuna Beacon": [763, 780], "Far Meridian": [459, 489]
            },
            routes: [
                ["Lacuna", "Mournstar", [450, 220]], ["Mournstar", "Lacuna Shoal", [670, 230]],
                ["Lacuna Shoal", "Penumbral Span", [900, 230]], ["Penumbral Span", "Mourn Relay", [1160, 450]],
                ["Mourn Relay", "Deep Vault", [1120, 750]], ["Deep Vault", "Lacuna Beacon", [875, 760]],
                ["Lacuna Beacon", "Far Meridian", [600, 720]], ["Far Meridian", "Lacuna", [350, 360]],
                ["Mournstar", "Penumbral Span", [780, 300]], ["Deep Vault", "Far Meridian", [760, 590]]
            ]
        },
        "Orison Fold": {
            asset: "/assets/galaxy/sector-orison-fold.webp?v=20260718-webp-1",
            galaxy: [1230, 782],
            galaxyContour: "M 1129 729 C 1152 697 1194 682 1232 688 C 1273 671 1323 687 1354 711 C 1396 722 1435 751 1444 786 C 1459 822 1438 860 1402 878 C 1375 917 1324 934 1281 920 C 1244 941 1191 931 1160 900 C 1119 884 1098 847 1109 812 C 1096 781 1105 749 1129 729 Z",
            systems: {
                "Orison": [408, 239], "Quietus": [718, 248], "Orison Lantern": [1128, 225], "Pale Coil": [1130, 472],
                "Orison Anchorage": [1120, 677], "Quiet Harbour": [969, 807], "Fold Meridian": [472, 793], "Pilgrim's Wake": [749, 489]
            },
            routes: [
                ["Orison", "Quietus", [560, 210]], ["Quietus", "Orison Lantern", [920, 300]],
                ["Orison Lantern", "Pale Coil", [1180, 340]], ["Pale Coil", "Orison Anchorage", [1090, 580]],
                ["Orison Anchorage", "Quiet Harbour", [1080, 760]], ["Quiet Harbour", "Fold Meridian", [720, 860]],
                ["Fold Meridian", "Pilgrim's Wake", [620, 650]], ["Pilgrim's Wake", "Orison", [470, 440]],
                ["Pale Coil", "Pilgrim's Wake", [950, 450]], ["Quiet Harbour", "Pilgrim's Wake", [880, 650]]
            ]
        },
        "Red Lattice": {
            asset: "/assets/galaxy/sector-red-lattice.webp?v=20260718-webp-1",
            galaxy: [696, 778],
            galaxyContour: "M 543 723 C 574 690 619 677 661 684 C 703 664 758 680 788 707 C 829 722 856 753 852 787 C 868 822 845 858 810 875 C 776 905 727 907 688 894 C 648 911 596 899 568 870 C 529 853 509 819 519 786 C 505 758 518 739 543 723 Z",
            systems: {
                "Red Lattice": [367, 177], "Sable Point": [593, 382], "Ternary": [1041, 225], "Crimson Needle": [1222, 500],
                "Crimson Relay": [1215, 785], "Sable Vault": [676, 744], "Ternary Watch": [420, 627], "Red Haven": [891, 510]
            },
            routes: [
                ["Red Lattice", "Sable Point", [500, 260]], ["Sable Point", "Ternary", [800, 260]],
                ["Ternary", "Crimson Needle", [1200, 330]], ["Crimson Needle", "Crimson Relay", [1180, 650]],
                ["Crimson Relay", "Sable Vault", [920, 820]], ["Sable Vault", "Ternary Watch", [540, 700]],
                ["Ternary Watch", "Red Haven", [620, 520]], ["Red Haven", "Red Lattice", [650, 300]],
                ["Sable Point", "Sable Vault", [520, 560]], ["Ternary", "Ternary Watch", [750, 380]]
            ]
        },
        "Umbral Marches": {
            asset: "/assets/galaxy/sector-umbral-marches.webp?v=20260718-webp-1",
            galaxy: [255, 784],
            galaxyContour: "M 105 735 C 133 706 175 694 215 702 C 253 684 306 696 337 721 C 377 732 403 761 401 794 C 415 827 394 862 360 879 C 328 913 279 923 238 908 C 201 929 151 917 121 890 C 82 875 61 841 72 808 C 58 779 77 750 105 735 Z",
            systems: {
                "Umbral Way": [280, 837], "Verdant Coil": [555, 778], "Umbral Lantern": [769, 421], "Shadow Cairn": [1097, 586],
                "Umbral Bastion": [1245, 202], "Viridian Refuge": [1154, 389], "Night Span": [971, 191], "Marcher Beacon": [940, 760]
            },
            routes: [
                ["Umbral Way", "Verdant Coil", [400, 800]], ["Verdant Coil", "Umbral Lantern", [650, 620]],
                ["Umbral Lantern", "Umbral Way", [500, 600]], ["Umbral Lantern", "Shadow Cairn", [950, 440]],
                ["Shadow Cairn", "Umbral Bastion", [1180, 390]], ["Umbral Bastion", "Viridian Refuge", [1250, 300]],
                ["Viridian Refuge", "Night Span", [1080, 260]], ["Night Span", "Marcher Beacon", [900, 480]],
                ["Marcher Beacon", "Shadow Cairn", [1020, 690]], ["Verdant Coil", "Marcher Beacon", [730, 850]]
            ]
        },
        "Warden Line": {
            asset: "/assets/galaxy/sector-warden-line.webp?v=20260718-webp-1",
            galaxy: [286, 178],
            galaxyContour: "M 119 105 C 153 62 202 40 254 40 C 307 25 374 38 418 62 C 470 78 511 116 522 155 C 541 196 517 241 476 263 C 437 297 378 307 329 294 C 280 315 218 301 176 273 C 129 258 99 222 106 183 C 91 153 99 124 119 105 Z",
            systems: {
                "Warden's Line": [340, 180], "Xanthe": [680, 255], "Yarrow": [1068, 328], "Warden Watch": [433, 449],
                "Zenith Yard": [1127, 589], "Sentinel Spur": [594, 675], "High Anchorage": [880, 756], "Northstar Gate": [1248, 757]
            },
            routes: [
                ["Warden's Line", "Xanthe", "M 340 180 C 455 208 565 280 680 255"],
                ["Xanthe", "Yarrow", "M 680 255 C 804 309 933 354 1068 328"],
                ["Yarrow", "Zenith Yard", "M 1068 328 C 1121 405 1145 510 1127 589"],
                ["Zenith Yard", "Northstar Gate", "M 1127 589 C 1162 652 1192 718 1248 757"],
                ["Northstar Gate", "High Anchorage", "M 1248 757 C 1131 756 1008 801 880 756"],
                ["High Anchorage", "Sentinel Spur", "M 880 756 C 770 731 680 690 594 675"],
                ["Sentinel Spur", "Warden Watch", "M 594 675 C 528 618 466 540 433 449"],
                ["Warden Watch", "Warden's Line", "M 433 449 C 409 337 432 257 340 180"],
                ["Xanthe", "Warden Watch", "M 680 255 C 554 326 476 385 433 449"],
                ["Warden Watch", "Zenith Yard", "M 433 449 C 648 426 923 445 1127 589"]
            ]
        }
    })
});
const twinReachesAtlas = Object.freeze({
    galaxyAsset: "/assets/galaxy/twin-reaches-overview.webp?v=20260720-training-atlas-1",
    galaxyRoutes: Object.freeze([]),
    sectors: Object.freeze({
        "Inner Reach": Object.freeze({
            asset: "/assets/galaxy/twin-reaches-inner-reach.webp?v=20260720-training-atlas-1"
        }),
        "Outer Reach": Object.freeze({
            asset: "/assets/galaxy/twin-reaches-outer-reach.webp?v=20260720-training-atlas-1"
        })
    })
});
const mapAtlasesByProfileKey = Object.freeze({
    "territorial-graph-v2": standardGalaxyAtlas,
    "tutorial-foundations-v1": twinReachesAtlas
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
    version: "v4",
    active: false,
    status: "available",
    storageKey: null,
    stepIndex: 0,
    initialTick: 0,
    briefing: null,
    completedActions: new Set(),
    target: null,
    targetDescribedBy: null,
    returnFocus: null,
    freshRequestId: null,
    dismissed: false,
    modal: false,
    inertElements: []
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
    gamesHome: document.querySelector("#gamesHome"),
    gamesHomeTitle: document.querySelector("#gamesHomeTitle"),
    gamesHomeSummary: document.querySelector("#gamesHomeSummary"),
    gamesHomeMessage: document.querySelector("#gamesHomeMessage"),
    gamesEmptyState: document.querySelector("#gamesEmptyState"),
    trainingOffer: document.querySelector("#trainingOffer"),
    trainingOfferCopy: document.querySelector("#trainingOfferCopy"),
    startTrainingButton: document.querySelector("#startTrainingButton"),
    trainingOfferMessage: document.querySelector("#trainingOfferMessage"),
    attentionSection: document.querySelector("#attentionSection"),
    attentionGames: document.querySelector("#attentionGames"),
    attentionMore: document.querySelector("#attentionMore"),
    activeGamesSection: document.querySelector("#activeGamesSection"),
    activeGames: document.querySelector("#activeGames"),
    activeGamesCount: document.querySelector("#activeGamesCount"),
    waitingGamesSection: document.querySelector("#waitingGamesSection"),
    waitingGames: document.querySelector("#waitingGames"),
    waitingGamesCount: document.querySelector("#waitingGamesCount"),
    completedGamesSection: document.querySelector("#completedGamesSection"),
    completedGames: document.querySelector("#completedGames"),
    completedGamesCount: document.querySelector("#completedGamesCount"),
    selectedGameContext: document.querySelector("#selectedGameContext"),
    allGamesLink: document.querySelector("#allGamesLink"),
    selectedGameKind: document.querySelector("#selectedGameKind"),
    selectedGameName: document.querySelector("#selectedGameName"),
    gameSelector: document.querySelector("#gameSelector"),
    viewNav: document.querySelector("#viewNav"),
    viewStack: document.querySelector("#viewStack"),
    turnProgressRibbon: document.querySelector("#turnProgressRibbon"),
    views: [...document.querySelectorAll("[data-view]")],
    viewLinks: [...document.querySelectorAll("[data-view-link]")],
    commandViewBadge: document.querySelector("#commandViewBadge"),
    galaxyViewBadge: document.querySelector("#galaxyViewBadge"),
    fleetsViewBadge: document.querySelector("#fleetsViewBadge"),
    historyViewBadge: document.querySelector("#historyViewBadge"),
    cycleStatus: document.querySelector("#cycleStatus"),
    nextTurnStatus: document.querySelector("#nextTurnStatus"),
    nextTurnTrack: document.querySelector("#nextTurnTrack"),
    turnProgressStatus: document.querySelector("#turnProgressStatus"),
    turnProgressTrack: document.querySelector("#turnProgressTrack"),
    empireName: document.querySelector("#empireName"),
    homeSystemName: document.querySelector("#homeSystemName"),
    commandView: document.querySelector("#commandView"),
    commandAgendaCount: document.querySelector("#commandAgendaCount"),
    commandPendingCount: document.querySelector("#commandPendingCount"),
    turnResolutionSection: document.querySelector("#turnResolutionSection"),
    turnResolutionTitle: document.querySelector("#turnResolutionTitle"),
    turnStageBadge: document.querySelector("#turnStageBadge"),
    turnStageDescription: document.querySelector("#turnStageDescription"),
    turnInitiativeNote: document.querySelector("#turnInitiativeNote"),
    turnPhaseOrder: document.querySelector("#turnPhaseOrder"),
    turnForecastSummary: document.querySelector("#turnForecastSummary"),
    councilAgenda: document.querySelector("#councilAgenda"),
    frontierSchematic: document.querySelector("#frontierSchematic"),
    commandStream: document.querySelector("#commandStream"),
    strategicWatchSummary: document.querySelector("#strategicWatchSummary"),
    resources: document.querySelector("#resources"),
    systemDetails: document.querySelector("#systemDetails"),
    systemsRoutesList: document.querySelector("#systemsRoutesList"),
    systemsRoutesSummary: document.querySelector("#systemsRoutesSummary"),
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
    fleetRosterSummary: document.querySelector("#fleetRosterSummary"),
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
    mapFocusHome: document.querySelector("#mapFocusHome"),
    mapFocusSelected: document.querySelector("#mapFocusSelected"),
    mapFocusFrontier: document.querySelector("#mapFocusFrontier"),
    mapMaximise: document.querySelector("#mapMaximise"),
    advanceTurnButton: document.querySelector("#advanceTurnButton"),
    commandAdvanceTurnButton: document.querySelector("#commandAdvanceTurnButton"),
    advanceTurnDialog: document.querySelector("#advanceTurnDialog"),
    advanceTurnDialogCounts: document.querySelector("#advanceTurnDialogCounts"),
    confirmAdvanceTurnButton: document.querySelector("#confirmAdvanceTurnButton"),
    turnMessage: document.querySelector("#turnMessage"),
    refreshButton: document.querySelector("#refreshButton"),
    tutorialButton: document.querySelector("#tutorialButton"),
    tutorialPanel: document.querySelector("#tutorialPanel"),
    tutorialKicker: document.querySelector("#tutorialKicker"),
    tutorialProgress: document.querySelector("#tutorialProgress"),
    tutorialCloseButton: document.querySelector("#tutorialCloseButton"),
    tutorialResetButton: document.querySelector("#tutorialResetButton"),
    tutorialAdmiralPortrait: document.querySelector("#tutorialAdmiralPortrait"),
    tutorialAdmiralRole: document.querySelector("#tutorialAdmiralRole"),
    tutorialAdmiralName: document.querySelector("#tutorialAdmiralName"),
    tutorialTitle: document.querySelector("#tutorialTitle"),
    tutorialBody: document.querySelector("#tutorialBody"),
    tutorialHint: document.querySelector("#tutorialHint"),
    tutorialRequirement: document.querySelector("#tutorialRequirement"),
    tutorialPauseButton: document.querySelector("#tutorialPauseButton"),
    tutorialSkipButton: document.querySelector("#tutorialSkipButton"),
    tutorialBackButton: document.querySelector("#tutorialBackButton"),
    tutorialNextButton: document.querySelector("#tutorialNextButton")
};

elements.loginForm.addEventListener("submit", async event => {
    event.preventDefault();
    await login(elements.username.value);
});

elements.signOutButton.addEventListener("click", signOut);
elements.startTrainingButton.addEventListener("click", startTraining);

elements.gameSelector.addEventListener("change", () => {
    const gameId = elements.gameSelector.value;
    const game = state.games.find(item => item.game.gameId === gameId);
    window.location.hash = selectedGameHash(game, state.activeView);
});

elements.refreshButton.addEventListener("click", refresh);

elements.commandAdvanceTurnButton.addEventListener("click", () => elements.advanceTurnButton.click());

elements.tutorialButton.addEventListener("click", startOrResumeTutorial);
elements.tutorialCloseButton.addEventListener("click", closeTutorialPanel);
elements.tutorialResetButton.addEventListener("click", resetTutorial);
elements.tutorialPauseButton.addEventListener("click", pauseTutorial);
elements.tutorialSkipButton.addEventListener("click", skipTutorial);
elements.tutorialBackButton.addEventListener("click", previousTutorialStep);
elements.tutorialNextButton.addEventListener("click", nextTutorialStep);

document.addEventListener("keydown", event => {
    if (tutorial.modal) {
        if (event.key === "Tab") {
            containTutorialFocus(event);
        } else if (event.key === "Escape") {
            event.preventDefault();
            closeTutorialPanel();
        }
        return;
    }

    const shortcutView = event.altKey && !event.ctrlKey && !event.metaKey
        ? viewShortcuts.get(event.key)
        : null;
    if (shortcutView && !elements.appShell.hidden && !elements.viewNav.hidden) {
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

    if (event.key === "Escape" && tutorial.active && !tutorial.dismissed) {
        event.preventDefault();
        closeTutorialPanel();
    }
});

window.addEventListener("hashchange", async () => {
    const route = routeFromHash();
    const changesSelectedGame = Boolean(state.gameId)
        && (route.kind !== "selected" || route.gameId !== state.gameId);
    if (changesSelectedGame && !confirmLeavingUnsavedPriorities()) {
        const selectedGame = gameById(state.gameId);
        const retainedHash = acceptedLocationHash || selectedGameHash(selectedGame, state.activeView);
        window.history.replaceState(null, "", retainedHash);
        renderGameSelector();
        return;
    }

    await navigateFromLocation({ focusHeading: true });
    acceptedLocationHash = window.location.hash;
});

window.addEventListener("beforeunload", event => {
    if (!hasUnsavedPriorityDraft()) {
        return;
    }

    event.preventDefault();
    event.returnValue = "";
});

elements.advanceTurnButton.addEventListener("click", async () => {
    if (isSelfPacedCycle()) {
        await resolveSelfPacedTurn();
        return;
    }

    if (tutorial.active
        && tutorial.briefing
        && state.cycle?.currentTickNumber === 0
        && !curatedObjectiveOrdersReady()) {
        setTurnMessage("Complete the three Day 1 commitments before closing the command window.");
        syncTutorialDisplay();
        return;
    }

    if (!commandsAreOpen()) {
        setTurnMessage(`${state.turnResolution?.stageLabel ?? "This turn"} does not accept player commands.`);
        return;
    }

    const turn = state.turnResolution;
    elements.advanceTurnDialogCounts.textContent = turn
        ? `Your queue: ${formatCount(turn.playerPendingOrderCount, "pending order")} · Current game: ${formatCount(turn.gamePendingHumanOrderCount, "human order")} queued · ${formatCount(turn.gameFleetIntentionCount, "fleet intention")} will be sealed.`
        : "The current game's complete fleet ledger will be sealed.";
    elements.advanceTurnDialog.returnValue = "";
    elements.advanceTurnDialog.showModal();
});

elements.advanceTurnDialog.addEventListener("close", async () => {
    if (elements.advanceTurnDialog.returnValue !== "confirm") {
        return;
    }

    await advanceTurn();
});

async function advanceTurn() {
    elements.advanceTurnButton.disabled = true;
    elements.commandAdvanceTurnButton.disabled = true;
    elements.confirmAdvanceTurnButton.disabled = true;
    try {
        const result = await gameApi.postJson("/admin/tick", {});
        setTurnMessage(`Published T${result.tickNumber}: ${formatCount(result.ordersProcessed, "sealed fleet intention")}, ${formatCount(result.eventsCreated, "event")}, ${formatCount(result.battlesCreated, "battle")}, ${formatCount(result.chronicleEntriesCreated, "Chronicle entry", "Chronicle entries")}. Display order did not grant initiative.`);
        await refresh();
    } catch (error) {
        setTurnMessage(error.message);
    } finally {
        elements.confirmAdvanceTurnButton.disabled = false;
        syncCommandWindowControls();
    }
}

async function resolveSelfPacedTurn() {
    const control = selfPacedTurnControl();
    if (!control.enabled) {
        setTurnMessage(control.blockedMessage);
        if (control.opensGuide) {
            await startOrResumeTutorial();
        }
        return;
    }

    elements.advanceTurnButton.disabled = true;
    elements.commandAdvanceTurnButton.disabled = true;
    try {
        const guided = control.mode === "guided";
        const result = guided
            ? await gameApi.postJson("/tutorial/resolve", {})
            : await gameApi.postJson(
                "/turns/resolve",
                { expectedCurrentTickNumber: state.cycle.currentTickNumber });
        if (guided) {
            state.tutorialJourney = result.journey;
        }
        setTurnMessage(
            `Published self-paced T${result.tickNumber}: ${formatCount(result.ordersProcessed, "fleet intention")}, ${formatCount(result.eventsCreated, "event")}, ${formatCount(result.battlesCreated, "battle")}.`);
        await refresh();
    } catch (error) {
        if (!isGameRequestCancellation(error)) {
            setTurnMessage(error.message);
        }
    } finally {
        syncCommandWindowControls();
    }
}

elements.commandView.addEventListener("click", async event => {
    const prioritiesButton = event.target.closest("[data-focus-priorities]");
    if (prioritiesButton) {
        elements.prioritySection.scrollIntoView({ behavior: "smooth", block: "center" });
        requestAnimationFrame(() => document.querySelector("#militaryWeight")?.focus({ preventScroll: true }));
        return;
    }

    const fleetButton = event.target.closest("[data-command-fleet]");
    if (fleetButton) {
        await selectFleet(fleetButton.dataset.commandFleet);
        if (fleetButton.dataset.commandAction) {
            activateFleetAction(fleetButton.dataset.commandAction);
            if (fleetButton.dataset.commandAction === "move") {
                selectCommandMoveTarget(fleetButton.dataset.commandTargetSystem);
            }
        }
        activateView("fleets", { updateLocation: true, focusHeading: true });
        return;
    }

    const systemButton = event.target.closest("[data-focus-system]");
    if (systemButton) {
        selectSystem(systemButton.dataset.focusSystem, { focusMap: true });
        activateView("galaxy", { updateLocation: true, focusHeading: true });
        requestAnimationFrame(() => elements.galaxyMap.focus({ preventScroll: true }));
    }
});

elements.commandView.addEventListener("keydown", event => {
    if (event.key !== "Enter" && event.key !== " ") {
        return;
    }

    const systemButton = event.target.closest("[data-focus-system]");
    if (systemButton) {
        event.preventDefault();
        systemButton.click();
    }
});

elements.galaxyMap.addEventListener("click", event => {
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
elements.mapMaximise.addEventListener("click", () => setMapMaximised(!state.mapMaximised));

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

elements.fleetDetails.addEventListener("click", async event => {
    const recallButton = event.target.closest("[data-recall-fleet-id]");
    if (recallButton) {
        await recallFleet(recallButton.dataset.recallFleetId);
        return;
    }

    const cancelButton = event.target.closest("[data-cancel-order-id]");
    if (cancelButton) {
        await cancelOrder(cancelButton.dataset.cancelOrderId);
    }
});

window.addEventListener("resize", () => {
    if (tutorial.active) {
        syncTutorialPresentation();
    }
});

elements.systemsRoutesList.addEventListener("click", event => {
    const destinationButton = event.target.closest("[data-topology-destination-id]");
    if (destinationButton) {
        selectSystem(destinationButton.dataset.topologyDestinationId, { restoreTopologyFocus: true });
        return;
    }

    const systemButton = event.target.closest("[data-topology-system-id]");
    if (systemButton) {
        selectSystem(systemButton.dataset.topologySystemId, { restoreTopologyFocus: true });
    }
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

elements.destinationSelect.addEventListener("change", renderMoveActionHint);

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
    if (!input || !state.priorityDraft || !commandsAreOpen()) {
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
    if (!commandsAreOpen()) {
        setPriorityMessage(`${state.turnResolution?.stageLabel ?? "This turn"} does not accept priority changes.`);
        return;
    }

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
        await gameApi.putJson("/priorities", payload);
        await refresh();
        setPriorityMessage("Priorities saved for the next tick.");
        completeTutorialAction("prioritiesSaved");
    } catch (error) {
        if (isGameRequestCancellation(error)) {
            return;
        }

        setPriorityMessage(error.message);
    } finally {
        state.prioritySaving = false;
        renderPriorityControls();
    }
});

elements.moveForm.addEventListener("submit", async event => {
    event.preventDefault();
    if (!commandsAreOpen()) {
        setMessage(`${state.turnResolution?.stageLabel ?? "This turn"} does not accept movement commands.`);
        return;
    }

    const fleetId = state.selectedFleetId;
    const targetSystemId = elements.destinationSelect.value;
    if (!fleetId || !targetSystemId) {
        setMessage("Select an active fleet with a linked destination.");
        return;
    }

    const targetName = selectedMoveDestination()?.systemName ?? "the selected system";
    const replacement = confirmOrderReplacement(fleetId, {
        orderType: "moveFleet",
        targetSystemId,
        targetEmpireId: null,
        targetFactionId: null,
        summary: `Move to ${targetName}`
    });
    if (!replacement) {
        return;
    }

    if (replacement.duplicate) {
        setMessage("That move is already the fleet's current intention.");
        return;
    }

    try {
        await gameApi.postJson("/orders/move", { fleetId, targetSystemId, replacesOrderId: replacement.replacesOrderId });
        setMessage(replacement.replacesOrderId ? "Move order replaced." : "Move order queued.");
        await refresh();
    } catch (error) {
        if (isGameRequestCancellation(error)) {
            return;
        }

        if (error.code === "stateConflict") {
            await refresh();
        }
        setMessage(error.message);
    }
});

elements.attackForm.addEventListener("submit", async event => {
    event.preventDefault();
    if (!commandsAreOpen()) {
        setMessage(`${state.turnResolution?.stageLabel ?? "This turn"} does not accept attack commands.`);
        return;
    }

    const fleetId = state.selectedFleetId;
    const targetFactionId = elements.targetEmpireSelect.value || null;
    const selectedFleet = state.fleets.find(item => item.fleet.fleetId === fleetId) ?? null;
    const targetFactions = collectTargetFactions(selectedFleet);
    if (!fleetId
        || !selectedFleet
        || selectedFleet.fleet.status !== "active"
        || selectedFleet.fleet.shipCount <= 0) {
        setMessage("Select an active fleet before attacking.");
        return;
    }

    if (targetFactions.length === 0) {
        setMessage("No hostile active fleet is present in this system.");
        return;
    }

    if (targetFactionId && !targetFactions.some(faction => faction.factionId === targetFactionId)) {
        setMessage("The selected hostile faction has no active fleet in this system.");
        return;
    }

    const targetName = elements.targetEmpireSelect.selectedOptions[0]?.textContent?.trim() || "nearest hostile";
    const replacement = confirmOrderReplacement(fleetId, {
        orderType: "attack",
        targetSystemId: null,
        targetEmpireId: null,
        targetFactionId,
        summary: `Attack ${targetName}`
    });
    if (!replacement) {
        return;
    }

    if (replacement.duplicate) {
        setMessage("That attack is already the fleet's current intention.");
        return;
    }

    try {
        await gameApi.postJson("/orders/attack", { fleetId, targetEmpireId: null, targetFactionId, replacesOrderId: replacement.replacesOrderId });
        setMessage(replacement.replacesOrderId ? "Attack order replaced." : "Attack order queued.");
        await refresh();
    } catch (error) {
        if (isGameRequestCancellation(error)) {
            return;
        }

        if (error.code === "stateConflict") {
            await refresh();
        }
        setMessage(error.message);
    }
});

elements.coloniseForm.addEventListener("submit", async event => {
    event.preventDefault();
    if (!commandsAreOpen()) {
        setMessage(`${state.turnResolution?.stageLabel ?? "This turn"} does not accept colonisation commands.`);
        return;
    }

    const fleetId = state.selectedFleetId;
    if (!fleetId) {
        setMessage("Select an active fleet outside its home system.");
        return;
    }

    const targetSystemId = state.fleetDetail?.currentSystem?.systemId ?? null;
    const targetName = state.fleetDetail?.currentSystem?.systemName ?? "the current system";
    const replacement = confirmOrderReplacement(fleetId, {
        orderType: "colonise",
        targetSystemId,
        targetEmpireId: null,
        targetFactionId: null,
        summary: `Colonise ${targetName}`
    });
    if (!replacement) {
        return;
    }

    if (replacement.duplicate) {
        setMessage("That colonisation is already the fleet's current intention.");
        return;
    }

    try {
        await gameApi.postJson("/orders/colonise", { fleetId, replacesOrderId: replacement.replacesOrderId });
        setMessage(replacement.replacesOrderId ? "Colonisation order replaced." : "Colonisation order queued.");
        await refresh();
    } catch (error) {
        if (isGameRequestCancellation(error)) {
            return;
        }

        if (error.code === "stateConflict") {
            await refresh();
        }
        setMessage(error.message);
    }
});

function confirmOrderReplacement(fleetId, proposedOrder) {
    const nextTick = (state.cycle?.currentTickNumber ?? 0) + 1;
    const pendingOrder = state.orders.find(order => order.fleetId === fleetId
        && order.status === "pending"
        && order.executeAfterTick === nextTick);
    if (!pendingOrder) {
        return { duplicate: false, replacesOrderId: null };
    }

    if (orderMatchesIntent(pendingOrder, proposedOrder)) {
        return { duplicate: true, replacesOrderId: null };
    }

    const confirmed = window.confirm(
        `Replace ${formatOrderIntent(pendingOrder)} with ${proposedOrder.summary}?\n\n` +
        "The previous order will remain in history as Superseded.");
    return confirmed
        ? { duplicate: false, replacesOrderId: pendingOrder.fleetOrderId }
        : null;
}

function orderMatchesIntent(order, proposedOrder) {
    return order.orderType === proposedOrder.orderType
        && (order.targetSystemId ?? null) === (proposedOrder.targetSystemId ?? null)
        && (order.targetEmpireId ?? null) === (proposedOrder.targetEmpireId ?? null)
        && (order.targetFactionId ?? null) === (proposedOrder.targetFactionId ?? null);
}

function formatOrderIntent(order) {
    const target = order.targetSystemName ?? order.targetFactionName ?? "nearest hostile";
    return `${formatOrderType(order.orderType)} ${target}`;
}

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
    try {
        await requireAntiforgeryToken();
        const session = await getJson("/auth/session");
        applyAccountSession(session);
        await loadGamesHome();
        await navigateFromLocation();
        acceptedLocationHash = window.location.hash;
    } catch (error) {
        if (!antiforgeryReady) {
            showLogin("Secure session setup failed. Refresh the page to try again.");
            return;
        }

        await loadTrustedPlayers();
        showLogin("Choose a player to continue.");
        elements.username.focus();
    }
}

async function loadTrustedPlayers() {
    const players = await getJson("/auth/trusted-players");
    const storedPlayerId = readStoredValue("cycles.playerId");
    fillSelect(elements.username, players, item => item.playerId, item => item.playerName);
    if (storedPlayerId && players.some(item => item.playerId === storedPlayerId)) {
        elements.username.value = storedPlayerId;
    }
}

async function login(playerId) {
    if (!playerId) {
        showLogin("Choose a player to continue.");
        elements.username.focus();
        return;
    }

    elements.loginButton.disabled = true;
    elements.loginMessage.textContent = "Signing in...";

    try {
        const login = await postJson("/auth/login", { playerId });
        clearAntiforgeryToken();
        await requireAntiforgeryToken();
        applyAccountSession(login);
        writeStoredValue("cycles.username", login.username);
        writeStoredValue("cycles.playerId", login.playerId);
        await loadGamesHome();
        window.history.replaceState(null, "", "#/games");
        acceptedLocationHash = window.location.hash;
        showGamesHome({ focusHeading: true });
    } catch (error) {
        showLogin(error.message);
    } finally {
        elements.loginButton.disabled = false;
    }
}

async function signOut() {
    elements.signOutButton.disabled = true;
    try {
        clearAntiforgeryToken();
        const requestToken = await requireAntiforgeryToken();
        const form = document.createElement("form");
        form.method = "post";
        form.action = "/auth/logout";
        form.hidden = true;

        const token = document.createElement("input");
        token.type = "hidden";
        token.name = antiforgeryFormFieldName;
        token.value = requestToken;
        form.append(token);
        document.body.append(form);
        form.submit();
    } catch (error) {
        elements.signOutButton.disabled = false;
        setMessage(error.message);
    }
}

function applyAccountSession(login) {
    const playerChanged = state.playerId !== login.playerId;
    if (state.playerId && playerChanged) {
        resetTutorialContext();
        clearSelectedGame();
    }
    if (playerChanged) {
        state.orderHistoryLimit = 20;
    }

    state.playerId = login.playerId;
    state.username = login.username;
    state.role = login.role;
    state.canAdvanceTurn = login.canAdvanceTurn ?? false;
    state.empire = login.empire ?? null;
    elements.advanceTurnButton.hidden = !state.canAdvanceTurn;
    elements.commandAdvanceTurnButton.hidden = !state.canAdvanceTurn;
    elements.sessionUsername.textContent = login.username;
    elements.loginForm.hidden = true;
    elements.sessionSummary.hidden = false;
    elements.appShell.hidden = false;
    document.body.classList.add("dashboard-active");
}

function applySession(login) {
    applyAccountSession(login);
    selectGame(login.gameId);
    activateFleetTab(state.fleetTab);
    activateFleetAction(state.fleetAction);
    activateHistoryTab(state.historyTab);
}

function showLogin(message) {
    clearSelectedGame();
    if (state.playerId) {
        resetTutorialContext();
    }

    state.playerId = null;
    state.username = null;
    state.role = null;
    state.canAdvanceTurn = false;
    state.gamesHome = null;
    state.games = [];
    state.empire = null;
    setMapMaximised(false);
    elements.loginMessage.textContent = message;
    elements.loginForm.hidden = false;
    elements.sessionSummary.hidden = true;
    elements.appHeaderControls.hidden = true;
    elements.appShell.hidden = true;
    elements.gamesHome.hidden = true;
    elements.selectedGameContext.hidden = true;
    document.body.classList.remove("dashboard-active");
    document.body.classList.remove("turn-ribbon-active");
    document.body.classList.remove("account-active");
}

function selectGame(gameId) {
    const selection = gameApi.selectGame(gameId);
    if (selection.changed) {
        clearGameScopedState();
    }

    state.gameId = selection.gameId;
    return selection;
}

function clearSelectedGame() {
    gameApi.clearGame();
    state.gameId = null;
    clearGameScopedState();
}

function clearGameScopedState() {
    state.cycle = null;
    state.empire = null;
    state.galaxy = null;
    state.selectedSystemId = null;
    state.selectedSectorId = null;
    state.selectedFleetId = null;
    state.fleetDetail = null;
    state.fleets = [];
    state.orders = [];
    state.events = [];
    state.chronicle = [];
    state.openingBriefing = null;
    state.tutorialJourney = null;
    state.turnResolution = null;
    state.priorityDraft = null;
    state.prioritySaving = false;
}

async function refresh({ applySessionFromBootstrap = false } = {}) {
    const selectedFleetQuery = state.selectedFleetId
        ? `?selectedFleetId=${encodeURIComponent(state.selectedFleetId)}`
        : "";
    const bootstrap = state.gameId
        ? await gameApi.getJson(`/dashboard/bootstrap${selectedFleetQuery}`)
        : await getJson(`/dashboard/bootstrap${selectedFleetQuery}`);
    if (!bootstrap?.gameId) {
        throw new Error("The dashboard bootstrap did not identify its Game.");
    }

    const bootstrapGameId = String(bootstrap.gameId).toLowerCase();
    if (!state.gameId) {
        selectGame(bootstrapGameId);
    } else if (bootstrapGameId !== state.gameId) {
        throw createGameRequestCancellation();
    }

    const { cycle, empire, galaxy, fleets, orders, events, chronicle, openingBriefing, turnResolution } = bootstrap;

    if (applySessionFromBootstrap) {
        applySession({ ...bootstrap.session, gameId: bootstrap.gameId, empire });
    }

    state.empire = empire;
    state.cycle = cycle;
    state.galaxy = galaxy;
    state.fleets = fleets;
    state.orders = orders;
    state.events = events;
    state.chronicle = chronicle;
    state.openingBriefing = openingBriefing;
    state.turnResolution = turnResolution;
    state.tutorialJourney = isTrainingGame()
        ? await gameApi.getJson("/tutorial/journey")
        : null;

    state.fleetDetail = bootstrap.selectedFleet;
    state.selectedFleetId = bootstrap.selectedFleet?.fleetId ?? null;

    if (!state.selectedSystemId || !galaxy.systems.some(system => system.systemId === state.selectedSystemId)) {
        state.selectedSystemId = empire.homeSystem.systemId;
    }
    renderCycle(cycle);
    renderTurnResolution(turnResolution);
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
    renderCommandWorkspace();
    syncCommandWindowControls();
    syncTutorialAfterRefresh();
    showSelectedGameShell(gameById(state.gameId));
}

async function loadGamesHome() {
    const home = await getJson("/games");
    state.gamesHome = home;
    const groupedGames = [
        ...(home.activeGames ?? []),
        ...(home.waitingGames ?? []),
        ...(home.completedGames ?? [])
    ];
    state.games = [...new Map(groupedGames.map(item => [item.game.gameId, item])).values()];
    renderGamesHome();
    renderGameSelector();
}

function renderGamesHome() {
    const home = state.gamesHome;
    if (!home) {
        return;
    }

    const total = state.games.length;
    elements.gamesHomeSummary.textContent = total === 0
        ? "No games enrolled"
        : `${formatCount(total, "game")} in your archive`;
    elements.gamesHomeMessage.textContent = home.hasMore
        ? "Showing the first 100 memberships. Older records remain safely paged."
        : "";
    elements.gamesEmptyState.hidden = total !== 0;
    elements.trainingOffer.hidden = !home.training;
    if (home.training) {
        elements.trainingOfferCopy.textContent =
            `About ${formatNumber(home.training.estimatedMinutes)} minutes. ` +
            "Your progress is a private game you can leave and resume, using the same mechanics as standard play.";
    }
    renderGamesHomeSection(
        elements.attentionSection,
        elements.attentionGames,
        home.needsAttention ?? [],
        { attention: true });
    const hiddenAttention = Math.max(0, (home.totalAttentionCount ?? 0) - (home.needsAttention?.length ?? 0));
    elements.attentionMore.textContent = hiddenAttention > 0
        ? `${hiddenAttention} more ${hiddenAttention === 1 ? "needs" : "need"} attention`
        : "";
    renderGamesHomeSection(
        elements.activeGamesSection,
        elements.activeGames,
        home.activeGames ?? []);
    elements.activeGamesCount.textContent = formatCount(home.activeGames?.length ?? 0, "game");
    renderGamesHomeSection(
        elements.waitingGamesSection,
        elements.waitingGames,
        home.waitingGames ?? []);
    elements.waitingGamesCount.textContent = formatCount(home.waitingGames?.length ?? 0, "game");
    renderGamesHomeSection(
        elements.completedGamesSection,
        elements.completedGames,
        home.completedGames ?? []);
    elements.completedGamesCount.textContent = formatCount(home.completedGames?.length ?? 0, "game");
}

async function startTraining() {
    const offer = state.gamesHome?.training;
    if (!offer) {
        return;
    }

    elements.startTrainingButton.disabled = true;
    elements.startTrainingButton.textContent = "Preparing…";
    elements.trainingOfferMessage.textContent = `Preparing ${offer.displayName}…`;
    try {
        const attempt = await postJson(
            `/training/${encodeURIComponent(offer.tutorialKey)}/attempts`,
            { requestId: crypto.randomUUID() });
        await loadGamesHome();
        const game = gameById(attempt.gameId);
        if (!game) {
            throw new Error("Training was prepared, but its Game is not yet visible. Refresh your game ledger.");
        }

        elements.trainingOfferMessage.textContent = attempt.created
            ? `${offer.displayName} is ready.`
            : `Resuming ${offer.displayName}.`;
        window.location.hash = selectedGameHash(game, "command");
    } catch (error) {
        elements.trainingOfferMessage.textContent = error.message;
    } finally {
        elements.startTrainingButton.disabled = false;
        elements.startTrainingButton.textContent = "Start Training";
    }
}

function renderGamesHomeSection(section, container, games, { attention = false } = {}) {
    section.hidden = games.length === 0;
    container.innerHTML = games.map(item => gameLedgerRow(item, { attention })).join("");
}

function gameLedgerRow(item, { attention = false } = {}) {
    const game = item.game;
    const playable = ["continue", "observe"].includes(item.action)
        && game.gameStatus === "active"
        && game.operationalCycleId;
    const actionLabel = {
        continue: "Continue",
        enterLobby: "Enter lobby",
        observe: "Observe",
        review: "Review"
    }[item.action] ?? "Open";
    const action = playable
        ? `<a class="game-row-action" href="${selectedGameHash(item, "command")}">${actionLabel}</a>`
        : `<span class="game-row-action is-unavailable" aria-disabled="true">${actionLabel}</span>`;
    const timing = game.nextTickAt
        ? new Date(game.nextTickAt) <= new Date()
            ? `Commands await resolution since ${formatAccountDate(game.nextTickAt)}`
            : `Commands open until ${formatAccountDate(game.nextTickAt)}`
        : game.currentTickNumber === null
            ? formatStatus(game.gameStatus)
            : `Cycle T${formatNumber(game.currentTickNumber)} · ${formatStatus(game.turnStage ?? game.operationalCycleStatus)}`;
    const attentionReason = attention && item.attentionReason
        ? `<span class="game-attention-reason">${escapeHtml(formatAttentionReason(item.attentionReason))}</span>`
        : "";

    return `
        <article class="game-ledger-row" data-game-id="${escapeHtml(game.gameId)}">
            <div class="game-ledger-identity">
                <span class="section-kicker">${escapeHtml(formatStatus(game.purpose))}</span>
                <h3>${escapeHtml(game.gameName)}</h3>
                <p>${escapeHtml(timing)}</p>
            </div>
            <div class="game-ledger-status">
                <span class="status-chip status-${escapeHtml(String(game.gameStatus).toLowerCase())}">${escapeHtml(formatStatus(game.gameStatus))}</span>
                <span>${escapeHtml(formatStatus(game.enrolmentStatus))}</span>
            </div>
            ${attentionReason}
            ${action}
        </article>`;
}

function formatAttentionReason(reason) {
    return {
        recoveryRequired: "Resolution needs recovery",
        commandsCloseSoon: "Command deadline",
        gameStarted: "Game recently started",
        trainingInProgress: "Training in progress"
    }[reason] ?? formatStatus(reason);
}

function formatAccountDate(value) {
    const date = new Date(value);
    return Number.isNaN(date.valueOf())
        ? "the published deadline"
        : date.toLocaleString([], { dateStyle: "medium", timeStyle: "short" });
}

function renderGameSelector() {
    const groups = [
        ["Active", state.gamesHome?.activeGames ?? []],
        ["Waiting", state.gamesHome?.waitingGames ?? []],
        ["Completed", state.gamesHome?.completedGames ?? []]
    ].filter(([, games]) => games.length > 0);
    elements.gameSelector.innerHTML = groups.map(([label, games]) =>
        `<optgroup label="${label}">${games.map(item =>
            `<option value="${escapeHtml(item.game.gameId)}">${escapeHtml(item.game.gameName)}</option>`).join("")}</optgroup>`
    ).join("");
    elements.gameSelector.hidden = state.games.length < 2;
    elements.gameSelector.closest("label").hidden = state.games.length < 2;
    if (state.gameId) {
        elements.gameSelector.value = state.gameId;
    }
}

function routeFromHash() {
    const value = window.location.hash.slice(1).replace(/^\//, "").toLowerCase();
    if (!value || value === "games") {
        return { kind: "home" };
    }

    const selected = value.match(/^games\/([0-9a-f-]{36})\/(command|galaxy|fleets|history)$/);
    if (selected && gameIdPattern.test(selected[1])) {
        return { kind: "selected", gameId: selected[1], view: selected[2] };
    }

    const legacyView = value === "chronicle" ? "history" : value;
    if (viewIds.includes(legacyView)) {
        const firstPlayable = state.games.find(item => item.action === "continue" || item.action === "observe");
        return firstPlayable
            ? { kind: "selected", gameId: firstPlayable.game.gameId, view: legacyView, legacy: true }
            : { kind: "home", legacy: true };
    }

    return { kind: "unavailable" };
}

function viewFromHash() {
    const route = routeFromHash();
    return route.kind === "selected" ? route.view : null;
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

async function navigateFromLocation({ focusHeading = false } = {}) {
    const route = routeFromHash();
    if (route.kind === "home") {
        if (route.legacy) {
            window.history.replaceState(null, "", "#/games");
        }
        showGamesHome({ focusHeading });
        return;
    }
    if (route.kind !== "selected") {
        showGamesHome({ focusHeading });
        elements.gamesHomeMessage.textContent = "Game unavailable. Return to your game ledger and choose an available game.";
        return;
    }

    const game = gameById(route.gameId);
    if (!game || !["continue", "observe"].includes(game.action)) {
        showGamesHome({ focusHeading });
        elements.gamesHomeMessage.textContent = "Game unavailable. Return to your game ledger and choose an available game.";
        return;
    }

    const selection = selectGame(route.gameId);
    showSelectedGameShell(game);
    activateView(route.view, { focusHeading });
    if (route.legacy) {
        window.history.replaceState(null, "", selectedGameHash(game, route.view));
    }
    if (selection.changed || !state.cycle) {
        try {
            await refresh();
        } catch (error) {
            if (!isGameRequestCancellation(error)) {
                setTurnMessage("Game unavailable. Its details could not be loaded for this account.");
            }
        }
    }
}

function showGamesHome({ focusHeading = false } = {}) {
    hideTutorialForAccount();
    clearSelectedGame();
    elements.gamesHome.hidden = false;
    elements.selectedGameContext.hidden = true;
    elements.viewNav.hidden = true;
    elements.viewStack.hidden = true;
    elements.turnProgressRibbon.hidden = true;
    elements.turnMessage.hidden = true;
    elements.appHeaderControls.hidden = true;
    document.body.classList.remove("turn-ribbon-active");
    document.body.classList.add("account-active");
    document.title = "Your games · Cycles";
    if (focusHeading) {
        requestAnimationFrame(() => elements.gamesHomeTitle.focus({ preventScroll: true }));
    }
}

function hideTutorialForAccount() {
    elements.tutorialPanel.hidden = true;
    document.body.classList.remove("tutorial-active");
    clearTutorialTarget();
    tutorial.dismissed = true;
    tutorial.returnFocus = null;
    syncTutorialPresentation();
}

function showSelectedGameShell(item) {
    if (!item) {
        return;
    }
    elements.gamesHome.hidden = true;
    elements.selectedGameContext.hidden = false;
    elements.viewNav.hidden = false;
    elements.viewStack.hidden = false;
    elements.turnProgressRibbon.hidden = isSelfPacedCycle() || isTrainingGame();
    elements.turnMessage.hidden = false;
    elements.appHeaderControls.hidden = false;
    elements.selectedGameName.textContent = item.game.gameName;
    elements.selectedGameKind.textContent = `${formatStatus(item.game.purpose)} game`;
    elements.gameSelector.value = item.game.gameId;
    for (const link of elements.viewLinks) {
        link.href = selectedGameHash(item, link.dataset.viewLink);
    }
    document.body.classList.remove("account-active");
    document.title = `${item.game.gameName} · ${formatStatus(state.activeView)} · Cycles`;
}

function selectedGameHash(item, view) {
    if (!item?.game?.gameId) {
        return "#/games";
    }
    const selectedView = viewIds.includes(view) ? view : "command";
    return `#/games/${encodeURIComponent(item.game.gameId)}/${selectedView}`;
}

function gameById(gameId) {
    return state.games.find(item => item.game.gameId === gameId) ?? null;
}

function confirmLeavingUnsavedPriorities() {
    return !hasUnsavedPriorityDraft()
        || window.confirm("Discard unsaved priority changes and leave this game?");
}

function hasUnsavedPriorityDraft() {
    return Boolean(state.empire && state.priorityDraft)
        && priorityKeys.some(key => state.priorityDraft[key] !== parseWeight(state.empire.priorities[key]));
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
    const selectedGame = gameById(state.gameId);
    const selectedHash = selectedGameHash(selectedGame, selectedView);
    if (updateLocation && window.location.hash !== selectedHash) {
        window.history.replaceState(null, "", selectedHash);
        acceptedLocationHash = window.location.hash;
    }
    if (selectedGame) {
        document.title = `${selectedGame.game.gameName} · ${formatStatus(selectedView)} · Cycles`;
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
    const stageLabel = state.turnResolution?.stageLabel ?? formatStatus(cycle.turnStage);
    const selfPaced = isSelfPacedCycle(cycle);
    elements.cycleStatus.innerHTML = `
        <span class="cycle-name">${escapeHtml(cycle.name)}</span>
        <span class="cycle-pill">T${cycle.currentTickNumber}</span>
        <span class="turn-stage-inline">${escapeHtml(stageLabel)}</span>
        ${statusChip(cycle.status)}
    `;
    elements.nextTurnStatus.textContent = selfPaced
        ? `${stageLabel} · T${cycle.currentTickNumber + 1} · Self-paced`
        : `${stageLabel} · T${cycle.currentTickNumber + 1} · ${formatNumber(cycle.tickLengthMinutes)}m cadence`;
    elements.nextTurnTrack.hidden = selfPaced;
    elements.turnProgressRibbon.hidden = selfPaced;
    document.body.classList.toggle("turn-ribbon-active", !selfPaced);
    if (!selfPaced) {
        renderTurnTimeline(cycle.currentTickNumber);
    }
}

function renderTurnResolution(turnResolution) {
    if (!turnResolution) {
        elements.turnStageBadge.textContent = formatStatus(state.cycle?.turnStage ?? "commandOpen");
        elements.turnStageDescription.textContent = "Turn-stage details are unavailable.";
        elements.turnPhaseOrder.innerHTML = "";
        elements.turnForecastSummary.innerHTML = `<article class="turn-forecast-item"><strong>Forecast unavailable</strong><span>Refresh the current game state.</span></article>`;
        return;
    }

    elements.turnResolutionTitle.textContent = `How T${formatNumber(turnResolution.nextTickNumber)} resolves`;
    elements.turnStageBadge.textContent = turnResolution.stageLabel;
    elements.turnStageBadge.className = `turn-stage-badge stage-${statusClass(turnResolution.stage)}`;
    elements.turnStageDescription.textContent = turnResolution.stageDescription;
    elements.turnInitiativeNote.textContent = turnResolution.submissionTimeGrantsInitiative
        ? "The current rules grant initiative by submission time."
        : "Submission time grants no initiative. Stable ordering only makes the sealed result reproducible.";
    elements.turnPhaseOrder.innerHTML = turnResolution.phases.map(phase => `
        <li data-turn-phase="${escapeHtml(phase.phase)}">
            <span>${String(phase.order).padStart(2, "0")}</span>
            <div>
                <strong>${escapeHtml(phase.title)}</strong>
                <p>${escapeHtml(phase.consequence)}</p>
            </div>
        </li>
    `).join("");

    const forecast = turnResolution.forecast;
    const income = forecast.expectedIncome;
    const reservation = forecast.colonisationReservation;
    const programme = forecast.automaticMilitaryProgramme;
    const deliveries = forecast.scheduledDeliveries;
    const deliverySummary = deliveries.length === 0
        ? "No authoritative ship deliveries are queued."
        : deliveries
            .map(delivery => `${formatCount(delivery.shipCount, "ship")} at T${formatNumber(delivery.deliveryTick)}`)
            .join(" · ");
    const reservationSummary = reservation.orderCount === 0
        ? "No Colonise Population reservation is projected."
        : `${formatCount(reservation.orderCount, "Colonise order")} require ${formatNumber(reservation.populationRequired)} Population; ${formatNumber(reservation.availablePopulationAfterIncome)} is projected after income.`;
    const programmeSummary = programme.projectedShipCount > 0
        ? `${formatCount(programme.projectedShipCount, "ship")} projected for delivery at T${formatNumber(programme.projectedDeliveryTick)}.`
        : "No automatic Military construction start is projected.";
    const progressionSummary = forecast.surveyProjectionExpectedNextWindow
        ? `Survey Projection is expected to unlock after T${formatNumber(turnResolution.nextTickNumber)} and apply in the next command window.`
        : "No new progression effect is currently projected for the next command window.";

    elements.turnForecastSummary.innerHTML = `
        <article class="turn-forecast-item is-projection">
            <small>Projected income · T${formatNumber(turnResolution.nextTickNumber)}</small>
            <strong>+${formatNumber(income.industry)} I · +${formatNumber(income.research)} R · +${formatNumber(income.population)} P</strong>
            <span>Calculated from current influence before movement.</span>
        </article>
        <article class="turn-forecast-item is-projection${reservation.isFullyFunded ? "" : " has-warning"}">
            <small>Projected reservation · closure</small>
            <strong>${formatNumber(reservation.populationRequired)} Population</strong>
            <span>${escapeHtml(reservationSummary)}</span>
        </article>
        <article class="turn-forecast-item is-projection">
            <small>Projected programme · phase 3</small>
            <strong>${formatNumber(programme.projectedIndustrySpend)} Industry · ${formatCount(programme.projectedShipCount, "ship")}</strong>
            <span>${escapeHtml(programmeSummary)}</span>
        </article>
        <article class="turn-forecast-item is-commitment">
            <small>Authoritative commitments</small>
            <strong>${formatCount(deliveries.reduce((total, delivery) => total + delivery.shipCount, 0), "queued ship")}</strong>
            <span>${escapeHtml(deliverySummary)}</span>
        </article>
        <article class="turn-forecast-item is-projection">
            <small>Projected progression · phase 8</small>
            <strong>${forecast.surveyProjectionExpectedNextWindow ? "Next-window unlock" : "No unlock projected"}</strong>
            <span>${escapeHtml(progressionSummary)}</span>
        </article>
    `;
}

function commandsAreOpen() {
    if (!antiforgeryReady || !state.gameId) {
        return false;
    }

    if (state.turnResolution) {
        return state.turnResolution.commandsAccepted;
    }

    return state.cycle?.turnStage === "commandOpen";
}

function syncCommandWindowControls() {
    const commandsOpen = commandsAreOpen();
    const stageLabel = state.turnResolution?.stageLabel ?? formatStatus(state.cycle?.turnStage ?? "commandOpen");
    const selfPaced = isSelfPacedCycle();
    const selfPacedControl = selfPaced ? selfPacedTurnControl() : null;
    const showDevelopmentAdvance = state.canAdvanceTurn && !selfPaced && !isTrainingGame();
    const showAdvance = showDevelopmentAdvance || selfPaced;
    elements.turnResolutionSection.classList.toggle("is-commands-closed", !commandsOpen);
    elements.advanceTurnButton.hidden = !showAdvance;
    elements.commandAdvanceTurnButton.hidden = !showAdvance;
    elements.advanceTurnButton.disabled = !commandsOpen || Boolean(selfPacedControl && !selfPacedControl.enabled);
    elements.commandAdvanceTurnButton.disabled = !commandsOpen || Boolean(selfPacedControl && !selfPacedControl.enabled);
    const advanceLabel = selfPacedControl?.label ?? "Close command window and advance";
    const advanceTitle = selfPacedControl?.title ?? "Close this game's command window and resolve one authoritative turn.";
    elements.advanceTurnButton.setAttribute("aria-label", advanceLabel);
    elements.advanceTurnButton.title = advanceTitle;
    elements.commandAdvanceTurnButton.textContent = advanceLabel;
    elements.commandAdvanceTurnButton.title = advanceTitle;

    for (const control of document.querySelectorAll("[data-cancel-order-id], [data-recall-fleet-id]")) {
        control.disabled = !commandsOpen;
        control.title = commandsOpen ? "" : `${stageLabel} does not accept command changes.`;
    }

    for (const form of [elements.moveForm, elements.attackForm, elements.coloniseForm]) {
        form.classList.toggle("is-command-closed", !commandsOpen);
        const submit = form.querySelector("button[type=submit]");
        if (!commandsOpen) {
            submit.disabled = true;
        }
    }

    renderPriorityControls();
}

function isSelfPacedCycle(cycle = state.cycle) {
    return String(cycle?.schedulingMode ?? "").replace(/[^a-z]/gi, "").toLowerCase() === "selfpaced";
}

function selfPacedTurnControl() {
    const journey = state.tutorialJourney;
    if (!journey) {
        return {
            mode: "free",
            enabled: commandsAreOpen(),
            label: "Resolve next turn",
            title: "Resolve this self-paced game's next authoritative turn now.",
            blockedMessage: "This self-paced turn is not accepting commands.",
            opensGuide: false
        };
    }

    const status = normaliseTutorialStatus(journey.journeyStatus);
    if (status === "recovery-required") {
        return {
            mode: "blocked",
            enabled: false,
            label: "Recovery required",
            title: "This Training attempt needs recovery before another turn can resolve.",
            blockedMessage: "This Training attempt needs recovery. Open the guide to start fresh.",
            opensGuide: true
        };
    }
    if (status === "paused") {
        return {
            mode: "blocked",
            enabled: false,
            label: "Resume Training guide",
            title: "Resume or skip the paused guide before resolving another Training turn.",
            blockedMessage: "Resume or skip the paused guide before resolving another Training turn.",
            opensGuide: true
        };
    }
    if (["completed", "skipped"].includes(status)) {
        return {
            mode: "free",
            enabled: commandsAreOpen(),
            label: "Resolve next turn",
            title: "Resolve this self-paced Training game's next authoritative turn now.",
            blockedMessage: "This self-paced turn is not accepting commands.",
            opensGuide: false
        };
    }

    return {
        mode: "guided",
        enabled: commandsAreOpen() && Boolean(journey.canResolve),
        label: "Resolve Training turn",
        title: journey.canResolve
            ? "Resolve the current guided Training turn now."
            : "Complete the current lesson commitment before resolving this Training turn.",
        blockedMessage: "Complete the current lesson commitment before resolving this Training turn.",
        opensGuide: true
    };
}

function renderTurnTimeline(currentTickNumber) {
    const tickNumber = Math.max(0, Math.trunc(Number(currentTickNumber) || 0));
    const boundedTickNumber = Math.min(cycleTurnLimit, tickNumber);
    const progress = (boundedTickNumber / cycleTurnLimit) * 100;
    const accessibleStatus = tickNumber > cycleTurnLimit
        ? `Turn ${tickNumber}; the ${cycleTurnLimit}-turn timeline is complete`
        : `Turn ${tickNumber} of ${cycleTurnLimit}`;

    elements.turnProgressStatus.textContent = `T${tickNumber} / ${cycleTurnLimit}`;
    elements.turnProgressTrack.style.setProperty("--turn-progress", `${progress}%`);
    elements.turnProgressTrack.setAttribute("aria-valuenow", String(boundedTickNumber));
    elements.turnProgressTrack.setAttribute("aria-valuetext", accessibleStatus);
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
    const operationalFleets = operationalFleetItems().length;
    const visibleEvents = state.events.length;
    const chronicleEntries = state.chronicle.length;
    const agenda = commandAgendaItems();
    const unaddressedAgendaItems = agenda.filter(item => item.tone === "urgent" || item.tone === "watch").length;
    const totalSystems = state.galaxy?.systems.length ?? 0;
    const controlledSystems = countControlledSystems(state.galaxy, state.empire?.empireId);

    setViewBadge(elements.commandViewBadge, unaddressedAgendaItems, `${formatCount(unaddressedAgendaItems, "unaddressed council agenda item")}`);
    setViewBadge(elements.galaxyViewBadge, controlledSystems, `${formatNumber(controlledSystems)} of ${formatCount(totalSystems, "system")} controlled`, `${formatNumber(controlledSystems)}/${formatNumber(totalSystems)}`);
    setViewBadge(elements.fleetsViewBadge, operationalFleets, `${formatCount(operationalFleets, "operational fleet")}`);
    setViewBadge(elements.historyViewBadge, visibleEvents + chronicleEntries, `${formatCount(visibleEvents + chronicleEntries, "historical record")}`);
    elements.commandPendingCount.textContent = formatNumber(pendingOrders);
}

function operationalFleetItems() {
    return state.fleets.filter(item =>
        (item.fleet.status === "active" || item.fleet.status === "inTransit")
        && item.fleet.shipCount > 0);
}

function transitCommitments() {
    return state.fleets
        .filter(item => item.fleet.status === "inTransit"
            && item.fleet.shipCount > 0
            && item.fleet.destinationSystemId
            && item.fleet.arrivalTickNumber !== null
            && item.fleet.arrivalTickNumber !== undefined
            && Number.isFinite(Number(item.fleet.arrivalTickNumber)))
        .map(item => {
            const processedMoves = state.orders
                .filter(order => order.status === "processed"
                    && order.orderType === "moveFleet"
                    && order.fleetId === item.fleet.fleetId)
                .sort((left, right) => Number(right.processedTick ?? -1) - Number(left.processedTick ?? -1)
                    || String(right.sealedAt ?? "").localeCompare(String(left.sealedAt ?? "")));
            const returning = item.fleet.destinationSystemId === item.fleet.currentSystemId;
            const moveOrder = returning
                ? processedMoves.find(order => order.targetSystemId !== item.fleet.currentSystemId)
                : processedMoves.find(order => order.targetSystemId === item.fleet.destinationSystemId);
            const processedRecall = state.orders
                .filter(order => order.status === "processed"
                    && order.orderType === "recallFleet"
                    && order.fleetId === item.fleet.fleetId)
                .sort((left, right) => Number(right.processedTick ?? -1) - Number(left.processedTick ?? -1))[0];
            const pendingRecall = state.orders.find(order => order.status === "pending"
                && order.orderType === "recallFleet"
                && order.fleetId === item.fleet.fleetId) ?? null;
            const departureTickNumber = Number(item.fleet.departureTickNumber ?? moveOrder?.processedTick);
            const recallExecuteTickNumber = pendingRecall ? Number(pendingRecall.executeAfterTick) : null;
            const projectedReturnArrivalTickNumber = pendingRecall && Number.isFinite(departureTickNumber)
                ? recallExecuteTickNumber + Math.max(1, recallExecuteTickNumber - departureTickNumber) - 1
                : null;
            const outwardDestinationSystemId = moveOrder?.targetSystemId
                ?? (returning ? null : item.fleet.destinationSystemId);
            return {
                fleetId: item.fleet.fleetId,
                fleetName: item.fleet.fleetName,
                sourceOrderId: moveOrder?.fleetOrderId ?? null,
                recallOrderId: processedRecall?.fleetOrderId ?? null,
                pendingRecallOrderId: pendingRecall?.fleetOrderId ?? null,
                dispatchedTickNumber: moveOrder?.processedTick ?? item.fleet.departureTickNumber ?? null,
                departureTickNumber: Number.isFinite(departureTickNumber) ? departureTickNumber : null,
                originSystemId: item.fleet.currentSystemId,
                originSystemName: item.currentSystemName,
                targetSystemId: item.fleet.destinationSystemId,
                targetSystemName: item.destinationSystemName ?? commandSystemName(item.fleet.destinationSystemId) ?? "Unknown destination",
                outwardDestinationSystemId,
                outwardDestinationSystemName: commandSystemName(outwardDestinationSystemId)
                    ?? (returning ? "Outbound destination" : item.destinationSystemName)
                    ?? "Unknown destination",
                arrivalTickNumber: Number(item.fleet.arrivalTickNumber),
                isReturning: returning,
                recallExecuteTickNumber,
                projectedReturnArrivalTickNumber
            };
        })
        .sort((left, right) => left.arrivalTickNumber - right.arrivalTickNumber
            || left.fleetName.localeCompare(right.fleetName));
}

function transitCommitmentForOrder(order) {
    if (order.status !== "processed" || (order.orderType !== "moveFleet" && order.orderType !== "recallFleet")) {
        return null;
    }

    return transitCommitments().find(item => item.isReturning
        ? item.recallOrderId === order.fleetOrderId
        : item.sourceOrderId === order.fleetOrderId) ?? null;
}

function transitEffectiveArrivalTick(transit) {
    return transit.projectedReturnArrivalTickNumber ?? transit.arrivalTickNumber;
}

function transitJourneyDuration(transit) {
    if (!transit || transit.departureTickNumber === null || transit.departureTickNumber === undefined) {
        return null;
    }

    return Math.max(1, transit.arrivalTickNumber - transit.departureTickNumber + 1);
}

function setViewBadge(element, value, label, displayValue = formatNumber(value)) {
    element.textContent = displayValue;
    element.setAttribute("aria-label", label);
}

function countControlledSystems(galaxy, empireId) {
    if (!galaxy || !empireId) {
        return 0;
    }

    const presenceBySystem = new Map(galaxy.presence.map(item => [item.systemId, item.effectivePresence]));
    return galaxy.systems.filter(system => {
        const presence = presenceBySystem.get(system.systemId) ?? {};
        const ownPresence = Number(presence[empireId] ?? 0);
        const strongestRivalPresence = Math.max(0, ...Object.entries(presence)
            .filter(([candidateEmpireId]) => candidateEmpireId !== empireId)
            .map(([, value]) => Number(value ?? 0)));
        return ownPresence > strongestRivalPresence;
    }).length;
}

function renderCommandWorkspace() {
    const agenda = commandAgendaItems().slice(0, 3);
    elements.commandAgendaCount.textContent = formatNumber(agenda.length);
    elements.councilAgenda.innerHTML = agenda.map((item, index) => `
        <article class="agenda-item agenda-tone-${item.tone}">
            <span class="agenda-index">${String(index + 1).padStart(2, "0")}</span>
            <span class="agenda-sigil" aria-hidden="true">${item.sigil}</span>
            <div class="agenda-copy">
                <small>${escapeHtml(item.category)}</small>
                <strong>${escapeHtml(item.title)}</strong>
                <span>${escapeHtml(item.detail)}</span>
            </div>
            <div class="agenda-timing">
                <small>Consequence</small>
                <span>${escapeHtml(item.consequence)}</span>
                <small>Timing</small>
                <span>${escapeHtml(item.timing)}</span>
            </div>
            ${item.action}
        </article>
    `).join("");

    renderFrontierSchematic();
    renderCommandStream();
    renderStrategicWatch();
}

function commandAgendaItems() {
    const briefing = state.openingBriefing;
    if (state.cycle?.currentTickNumber === 0
        && briefing?.objectives?.move
        && briefing.objectives.colonise
        && briefing.objectives.attack) {
        return [
            commandObjectiveAgendaItem({
                category: "Fleet command",
                orderType: "attack",
                fleetId: briefing.objectives.attack.fleetId,
                targetSystemId: briefing.objectives.attack.systemId,
                targetEmpireId: briefing.objectives.attack.targetEmpireId,
                targetFactionId: briefing.objectives.attack.targetFactionId,
                detail: "Hostile force in local space",
                consequence: "Engagement resolves",
                sigil: "X"
            }),
            commandObjectiveAgendaItem({
                category: "Fleet movement",
                orderType: "moveFleet",
                fleetId: briefing.objectives.move.fleetId,
                targetSystemId: briefing.objectives.move.targetSystemId,
                detail: "Frontier station requires reinforcement",
                consequence: "Fleet changes station",
                sigil: "N"
            }),
            commandObjectiveAgendaItem({
                category: "Expansion",
                orderType: "colonise",
                fleetId: briefing.objectives.colonise.fleetId,
                targetSystemId: briefing.objectives.colonise.systemId,
                detail: "Population-funded outpost opportunity",
                consequence: "100 population reserved at closure",
                sigil: "O"
            })
        ];
    }

    const pendingOrders = state.orders.filter(order => order.status === "pending");
    const transits = transitCommitments();
    const activeFleets = state.fleets.filter(item => item.fleet.status === "active" && item.fleet.shipCount > 0);
    const committedFleetIds = new Set(pendingOrders.map(order => order.fleetId));
    const uncommittedFleets = activeFleets.filter(item => !committedFleetIds.has(item.fleet.fleetId));
    const nextTick = (state.cycle?.currentTickNumber ?? 0) + 1;
    const priorities = state.empire?.priorities ?? { militaryWeight: 0, expansionWeight: 0 };
    const forecast = state.turnResolution?.forecast ?? null;
    const hasScheduledEffects = Boolean(forecast?.hasScheduledEffects);
    const commitmentCount = new Set([
        ...pendingOrders.map(order => order.fleetId),
        ...transits.map(transit => transit.fleetId)
    ]).size;
    const nextArrivalTick = transits
        .map(transitEffectiveArrivalTick)
        .sort((left, right) => left - right)[0] ?? null;
    const queueTitle = pendingOrders.length > 0
        ? formatCount(commitmentCount, "ongoing commitment")
        : transits.length > 0
            ? formatCount(transits.length, "journey underway", "journeys underway")
            : "No player orders queued";
    const queueDetail = commitmentCount === 0
        ? hasScheduledEffects
            ? "Automatic income, programmes, or deliveries still resolve"
            : "No player orders or scheduled effects"
        : `${formatCount(pendingOrders.length, "order")} queued · ${formatCount(transits.length, "fleet")} in transit`;
    const queueConsequence = pendingOrders.length > 0
        ? `New orders process from T${nextTick}`
        : nextArrivalTick !== null
            ? `Transit continues automatically · next arrival T${nextArrivalTick}`
            : hasScheduledEffects
                ? `Scheduled effects resolve at T${nextTick}`
                : "Uncommitted fleets Hold";

    return [
        {
            category: "Order queue",
            title: queueTitle,
            detail: queueDetail,
            consequence: queueConsequence,
            timing: `T${state.cycle?.currentTickNumber ?? 0} → T${nextTick}`,
            tone: commitmentCount === 0 ? hasScheduledEffects ? "watch" : "urgent" : "queued",
            sigil: commitmentCount === 0 ? hasScheduledEffects ? "S" : "!" : "Q",
            action: `<a class="agenda-action" href="#fleets">${commitmentCount === 0 ? "Issue orders" : "Review commitments"}</a>`
        },
        {
            category: "Fleet readiness",
            title: formatCount(uncommittedFleets.length, "fleet awaits direction", "fleets await direction"),
            detail: `${formatCount(activeFleets.length, "active fleet")} available · ${formatCount(transits.length, "fleet")} in transit`,
            consequence: uncommittedFleets.length > 0
                ? "Uncommitted fleets hold"
                : transits.length > 0
                    ? "Journeys continue automatically"
                    : "All active fleets committed",
            timing: `Before T${nextTick}`,
            tone: uncommittedFleets.length === 0 ? "resolved" : "watch",
            sigil: "F",
            action: `<a class="agenda-action" href="#fleets">Open fleets</a>`
        },
        {
            category: "Strategic programmes",
            title: `Military ${priorities.militaryWeight} · Expansion ${priorities.expansionWeight}`,
            detail: forecast?.automaticMilitaryProgramme?.projectedShipCount > 0
                ? `${formatNumber(forecast.automaticMilitaryProgramme.projectedIndustrySpend)} Industry projected for ${formatCount(forecast.automaticMilitaryProgramme.projectedShipCount, "ship")}`
                : "The active allocation applies at the next turn",
            consequence: forecast?.automaticMilitaryProgramme?.projectedDeliveryTick
                ? `Projected delivery T${forecast.automaticMilitaryProgramme.projectedDeliveryTick}`
                : `Applies at T${nextTick}`,
            timing: `T${state.cycle?.currentTickNumber ?? 0} → T${nextTick}`,
            tone: "programme",
            sigil: "S",
            action: `<button class="agenda-action" type="button" data-focus-priorities>Review allocation</button>`
        }
    ];
}

function commandObjectiveAgendaItem({ category, orderType, fleetId, targetSystemId, targetEmpireId = null, targetFactionId = null, detail, consequence, sigil }) {
    const order = state.orders
        .filter(candidate => candidate.fleetId === fleetId && String(candidate.orderType).toLowerCase() === orderType.toLowerCase())
        .filter(candidate => !targetSystemId || !candidate.targetSystemId || candidate.targetSystemId === targetSystemId)
        .filter(candidate => !targetEmpireId || !candidate.targetEmpireId || candidate.targetEmpireId === targetEmpireId)
        .filter(candidate => !targetFactionId || !candidate.targetFactionId || candidate.targetFactionId === targetFactionId)
        .sort((left, right) => Number(right.status === "pending") - Number(left.status === "pending")
            || Number(right.executeAfterTick ?? 0) - Number(left.executeAfterTick ?? 0))[0];
    const status = String(order?.status ?? "").toLowerCase();
    const queued = status === "pending";
    const resolved = status === "processed" || status === "completed";
    const nextTick = (state.cycle?.currentTickNumber ?? 0) + 1;
    const targetName = commandSystemName(targetSystemId) ?? commandFleetName(fleetId);
    const actionLabel = queued ? "Review order" : resolved ? "Review history" : `Issue ${formatOrderType(orderType).toLowerCase()}`;
    const commandTarget = orderType === "moveFleet" && targetSystemId
        ? ` data-command-target-system="${escapeHtml(targetSystemId)}"`
        : "";
    const action = resolved
        ? `<a class="agenda-action" href="#history">${actionLabel}</a>`
        : `<button class="agenda-action" type="button" data-command-fleet="${fleetId}" data-command-action="${orderType === "moveFleet" ? "move" : orderType}"${commandTarget}>${actionLabel}</button>`;

    return {
        category,
        title: targetName,
        detail: queued ? `Queued: ${commandFleetName(fleetId)}` : resolved ? `Resolved: ${commandFleetName(fleetId)}` : detail,
        consequence: queued ? `Processes from T${order.executeAfterTick}` : resolved ? `Recorded at T${order.processedTick ?? "?"}` : consequence,
        timing: `T${state.cycle?.currentTickNumber ?? 0} → T${nextTick}`,
        tone: queued ? "queued" : resolved ? "resolved" : "urgent",
        sigil,
        action
    };
}

function commandFleetName(fleetId) {
    return state.fleets.find(item => item.fleet.fleetId === fleetId)?.fleet.fleetName ?? "Fleet objective";
}

function commandSystemName(systemId) {
    return state.galaxy?.systems.find(system => system.systemId === systemId)?.systemName ?? null;
}

function renderFrontierSchematic() {
    const systems = commandFrontierSystems();
    if (systems.length === 0) {
        elements.frontierSchematic.innerHTML = `<p class="command-empty">No frontier systems are available.</p>`;
        return;
    }

    const positions = [
        { x: 320, y: 72 },
        { x: 118, y: 272 },
        { x: 522, y: 272 },
        { x: 320, y: 318 }
    ];
    const plotted = systems.map((system, index) => ({ ...system, ...positions[index] }));
    const plottedById = new Map(plotted.map(system => [system.systemId, system]));
    const routes = (state.galaxy?.links ?? [])
        .filter(link => plottedById.has(link.systemAId) && plottedById.has(link.systemBId))
        .map(link => {
            const start = plottedById.get(link.systemAId);
            const end = plottedById.get(link.systemBId);
            return `<path class="schematic-route${commandRouteHasCommitment(link) ? " is-queued" : ""}" d="M ${start.x} ${start.y} L ${end.x} ${end.y}"></path>`;
        }).join("");
    const attackSystemId = state.openingBriefing?.objectives?.attack?.systemId;
    const committedTargets = new Set([
        ...state.orders.filter(order => order.status === "pending").map(order => order.targetSystemId),
        ...transitCommitments().map(transit => transit.isReturning ? transit.originSystemId : transit.outwardDestinationSystemId)
    ].filter(Boolean));
    const nodes = plotted.map(system => {
        const isHome = system.systemId === state.empire?.homeSystem.systemId;
        const tone = isHome ? "home" : system.systemId === attackSystemId ? "threat" : committedTargets.has(system.systemId) ? "queued" : "frontier";
        const labelY = system.y < 120 ? system.y - 34 : system.y + 48;
        return `
            <g class="schematic-node is-${tone}" data-focus-system="${system.systemId}" role="link" tabindex="0" aria-label="Open ${escapeHtml(system.systemName)} in Map">
                <circle class="schematic-node-orbit" cx="${system.x}" cy="${system.y}" r="28"></circle>
                <circle class="schematic-node-core" cx="${system.x}" cy="${system.y}" r="9"></circle>
                <circle class="schematic-node-point" cx="${system.x}" cy="${system.y}" r="3"></circle>
                <text x="${system.x}" y="${labelY}" text-anchor="middle">${escapeHtml(system.systemName)}</text>
            </g>
        `;
    }).join("");

    elements.frontierSchematic.innerHTML = `
        <svg viewBox="0 0 640 380" preserveAspectRatio="xMidYMin meet" role="group" aria-label="Current command frontier">
            <g class="schematic-grid" aria-hidden="true">
                <path d="M 0 95 H 640 M 0 190 H 640 M 0 285 H 640"></path>
                <path d="M 160 0 V 380 M 320 0 V 380 M 480 0 V 380"></path>
            </g>
            <g class="schematic-routes">${routes}</g>
            <g class="schematic-nodes">${nodes}</g>
        </svg>
        <div class="schematic-legend" aria-hidden="true">
            <span class="legend-home">Home</span>
            <span class="legend-queued">Committed route</span>
            <span class="legend-threat">Unresolved objective</span>
        </div>
    `;
}

function commandFrontierSystems() {
    const systemIds = [];
    const add = value => {
        if (value && !systemIds.includes(value)) {
            systemIds.push(value);
        }
    };
    add(state.empire?.homeSystem.systemId);
    add(state.openingBriefing?.objectives?.move?.targetSystemId);
    add(state.openingBriefing?.objectives?.colonise?.systemId);
    add(state.openingBriefing?.objectives?.attack?.systemId);
    for (const order of state.orders.filter(order => order.status === "pending")) {
        add(order.targetSystemId);
    }
    for (const transit of transitCommitments()) {
        add(transit.outwardDestinationSystemId);
    }
    if (systemIds.length < 4 && state.empire?.homeSystem.systemId) {
        for (const system of linkedSystems(state.empire.homeSystem.systemId)) {
            add(system.systemId);
        }
    }

    const systemsById = new Map((state.galaxy?.systems ?? []).map(system => [system.systemId, system]));
    return systemIds.slice(0, 4).map(systemId => systemsById.get(systemId)).filter(Boolean);
}

function commandRouteHasCommitment(link) {
    const hasPendingOrder = state.orders.some(order => order.status === "pending"
        && order.targetSystemId
        && (order.targetSystemId === link.systemAId || order.targetSystemId === link.systemBId));
    const hasTransit = transitCommitments().some(transit => {
        const fleet = state.fleets.find(item => item.fleet.fleetId === transit.fleetId)?.fleet;
        return fleet
            && ((fleet.currentSystemId === link.systemAId && transit.outwardDestinationSystemId === link.systemBId)
                || (fleet.currentSystemId === link.systemBId && transit.outwardDestinationSystemId === link.systemAId));
    });
    return hasPendingOrder || hasTransit;
}

function renderCommandStream() {
    const activeFleets = state.fleets.filter(item => item.fleet.status === "active" && item.fleet.shipCount > 0).length;
    const pendingOrders = state.orders.filter(order => order.status === "pending").length;
    const transits = transitCommitments().length;
    const latestEvents = state.events
        .slice()
        .sort((left, right) => right.tickNumber - left.tickNumber || String(right.createdAt).localeCompare(String(left.createdAt)))
        .slice(0, 2)
        .map(event => ({ marker: `T${event.tickNumber}`, text: event.displayText, tone: statusClass(event.severity) }));
    const rows = [
        ...latestEvents,
        { marker: "F", text: formatCount(activeFleets, "active fleet") + " ready", tone: "fleet" },
        { marker: "Q", text: `${formatCount(pendingOrders, "order")} pending · ${formatCount(transits, "fleet")} in transit`, tone: pendingOrders + transits > 0 ? "queued" : "quiet" }
    ].slice(0, 4);

    elements.commandStream.innerHTML = rows.map(row => `
        <div class="command-stream-entry stream-tone-${row.tone}">
            <span>${escapeHtml(row.marker)}</span>
            <p>${escapeHtml(row.text)}</p>
        </div>
    `).join("");
}

function renderStrategicWatch() {
    const resources = state.empire?.resources;
    const priorities = state.empire?.priorities;
    const activeFleets = state.fleets.filter(item => item.fleet.status === "active" && item.fleet.shipCount > 0).length;
    const transits = transitCommitments().length;
    const totalFleets = state.fleets.length;
    const outposts = state.galaxy?.colonialOutposts?.filter(outpost => outpost.empireId === state.empire?.empireId).length ?? 0;
    const research = Number(resources?.research ?? 0);
    const researchProgress = Math.min(100, Math.round((research / 200) * 100));

    elements.strategicWatchSummary.innerHTML = `
        <div><dt>Programme</dt><dd>Military ${formatNumber(priorities?.militaryWeight ?? 0)} · Expansion ${formatNumber(priorities?.expansionWeight ?? 0)}</dd></div>
        <div><dt>Doctrine</dt><dd>Survey Projection · ${formatNumber(research)} / 200 Research <span>${researchProgress}%</span></dd></div>
        <div><dt>Fleet posture</dt><dd>${formatNumber(activeFleets)} active · ${formatNumber(transits)} in transit · ${formatNumber(totalFleets)} recorded</dd></div>
        <div><dt>Expansion</dt><dd>${formatCount(outposts, "outpost")} · ${formatNumber(resources?.population ?? 0)} Population</dd></div>
    `;
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
    const activeFleetCount = fleets.filter(item => item.fleet.status === "active" && item.fleet.shipCount > 0).length;
    const transitFleetCount = fleets.filter(item => item.fleet.status === "inTransit" && item.fleet.shipCount > 0).length;
    elements.fleetRosterSummary.textContent = `${formatNumber(activeFleetCount)} active · ${formatNumber(transitFleetCount)} in transit`;
    elements.fleets.innerHTML = fleets.length === 0
        ? `<article class="item"><span>No fleets yet.</span></article>`
        : fleets.slice().sort((left, right) => {
            const leftRank = left.fleet.status === "active" ? 0 : left.fleet.status === "inTransit" ? 1 : 2;
            const rightRank = right.fleet.status === "active" ? 0 : right.fleet.status === "inTransit" ? 1 : 2;
            return leftRank - rightRank
                || left.fleet.fleetName.localeCompare(right.fleet.fleetName);
        }).map(item => {
        const fleet = item.fleet;
        const inTransit = fleet.status === "inTransit";
        const transit = inTransit ? transitCommitments().find(candidate => candidate.fleetId === fleet.fleetId) ?? null : null;
        const position = transit?.isReturning
            ? `${transit.outwardDestinationSystemName} route · returning to ${transit.originSystemName}`
            : inTransit && item.destinationSystemName
                ? `${item.currentSystemName} → ${item.destinationSystemName}`
            : item.currentSystemName;
        const transitSummary = transit?.pendingRecallOrderId
            ? `<span class="fleet-transit-summary">Projected reversal T${formatTickNumber(transit.recallExecuteTickNumber)} · projected return T${formatTickNumber(transit.projectedReturnArrivalTickNumber)}</span>`
            : transit?.isReturning
                ? `<span class="fleet-transit-summary">Recalled T${formatTickNumber(transit.departureTickNumber)} · arrives T${formatTickNumber(transit.arrivalTickNumber)}</span>`
                : inTransit
                    ? `<span class="fleet-transit-summary">Dispatched T${formatTickNumber(transit?.dispatchedTickNumber)} · arrives T${formatTickNumber(fleet.arrivalTickNumber)} · recall available</span>`
            : "";
        const selectedClass = fleet.fleetId === state.selectedFleetId ? " selected" : "";
        const transitClass = inTransit ? " is-in-transit" : "";
        const admiral = item.admiral ? `<span>${escapeHtml(formatAdmiral(item.admiral))}</span>` : "";
        return `
            <article class="item fleet-item${selectedClass}${transitClass}" data-fleet-id="${fleet.fleetId}" role="button" tabindex="0">
                <strong>${escapeHtml(fleet.fleetName)}</strong>
                <span class="item-meta">
                    ${statusChip(fleet.status)}
                    <span>${fleet.shipCount} ships</span>
                    <span>${escapeHtml(position)}</span>
                    ${transitSummary}
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

    const transitCommitment = detail.status === "inTransit"
        ? transitCommitments().find(item => item.fleetId === detail.fleetId) ?? null
        : null;
    const positionRows = transitCommitment?.isReturning
        ? `
            <dt>Route</dt><dd>${escapeHtml(transitCommitment.originSystemName)} ↔ ${escapeHtml(transitCommitment.outwardDestinationSystemName)}</dd>
            <dt>Returning to</dt><dd>${escapeHtml(transitCommitment.originSystemName)}</dd>
            <dt>Arrival</dt><dd>T${formatNumber(transitCommitment.arrivalTickNumber)}</dd>
        `
        : transitCommitment
            ? `
                <dt>Departed from</dt><dd>${escapeHtml(transitCommitment.originSystemName)}</dd>
                <dt>Destination</dt><dd>${escapeHtml(transitCommitment.outwardDestinationSystemName)}</dd>
                <dt>Arrival</dt><dd>T${formatNumber(transitCommitment.arrivalTickNumber)}</dd>
            `
        : `
            <dt>Current</dt><dd>${escapeHtml(detail.currentSystem.systemName)}</dd>
            <dt>Strategic</dt><dd>${detail.currentSystem.strategicValue}</dd>
            <dt>History</dt><dd>${detail.currentSystem.historicalSignificance}</dd>
        `;

    const linkedSystems = detail.linkedSystems.length === 0
        ? `<span>No adjacent systems.</span>`
        : detail.linkedSystems.map(system => `<span>${escapeHtml(system.systemName)} (${system.strategicValue})</span>`).join("");

    const nearbyFleets = detail.activeFleetsInSystem.length === 0
        ? `<span>No other active fleets at this system.</span>`
        : detail.activeFleetsInSystem.map(fleet => {
            const admiral = fleet.admiral ? ` | ${formatAdmiral(fleet.admiral)}` : "";
            return `
            <span>${escapeHtml(fleet.fleetName)} | ${escapeHtml(fleet.factionName)} | ${fleet.shipCount} ships${escapeHtml(admiral)}</span>
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
    const pendingRecall = pendingOrders.find(order => order.orderType === "recallFleet") ?? null;
    const resolvedOrderCount = detail.orders.length - pendingOrders.length;
    const journeyTicks = transitJourneyDuration(transitCommitment);
    const orders = pendingRecall && transitCommitment
        ? `<span><strong>Recall ordered</strong> · projected reversal T${formatTickNumber(pendingRecall.executeAfterTick)} · projected return T${formatTickNumber(transitCommitment.projectedReturnArrivalTickNumber)}.</span>`
        : transitCommitment?.isReturning
            ? `<span><strong>Recall underway</strong> · returning to ${escapeHtml(transitCommitment.originSystemName)} · recalled T${formatTickNumber(transitCommitment.departureTickNumber)} · arrives T${formatNumber(transitCommitment.arrivalTickNumber)}.</span>`
            : transitCommitment
                ? `<span><strong>Move underway</strong> · ${escapeHtml(transitCommitment.outwardDestinationSystemName)} · ${escapeHtml(formatJourneyDuration(journeyTicks ?? 1))} · dispatched T${formatTickNumber(transitCommitment.dispatchedTickNumber)} · arrives T${formatNumber(transitCommitment.arrivalTickNumber)}. Transit continues automatically unless recalled.</span>`
        : pendingOrders.length === 0
            ? `<span>No current intention. This fleet is available for a command.</span>`
            : pendingOrders.map(order => {
            const target = order.targetSystemName ?? order.targetFactionName ?? "nearest hostile";
            const timing = formatOrderTiming(order);
            return `<span>${escapeHtml(formatOrderType(order.orderType))} | ${escapeHtml(target)} | ${escapeHtml(timing)}</span>`;
        }).join("");

    const journeyProgress = journeyTicks === null
        ? null
        : Math.min(100, Math.max(0, Math.round((((state.cycle?.currentTickNumber ?? 0) - transitCommitment.departureTickNumber + 1) / journeyTicks) * 100)));
    const journeyRoute = transitCommitment?.isReturning
        ? `${transitCommitment.outwardDestinationSystemName} route · returning to ${transitCommitment.originSystemName}`
        : transitCommitment
            ? `${transitCommitment.originSystemName} → ${transitCommitment.outwardDestinationSystemName}`
            : "";
    const journeyTiming = pendingRecall && transitCommitment
        ? `Projected reversal T${formatTickNumber(pendingRecall.executeAfterTick)} · projected return T${formatTickNumber(transitCommitment.projectedReturnArrivalTickNumber)}`
        : transitCommitment?.isReturning
            ? `Recalled T${formatTickNumber(transitCommitment.departureTickNumber)} · arrives T${formatTickNumber(transitCommitment.arrivalTickNumber)}`
            : transitCommitment
                ? `${formatJourneyDuration(journeyTicks ?? 1)} · dispatched T${formatTickNumber(transitCommitment.dispatchedTickNumber)} · arrives T${formatTickNumber(transitCommitment.arrivalTickNumber)}`
                : "";
    const journeyAction = pendingRecall
        ? `<button type="button" class="inline-action" data-cancel-order-id="${pendingRecall.fleetOrderId}">Cancel recall</button>`
        : transitCommitment?.isReturning
            ? ""
            : transitCommitment
                ? `<button type="button" class="inline-action" data-recall-fleet-id="${detail.fleetId}">Recall to ${escapeHtml(transitCommitment.originSystemName)}</button>`
                : "";
    const journeyBlock = transitCommitment
        ? `
            <div class="detail-block fleet-journey-block">
                <strong>Journey</strong>
                <span>${escapeHtml(journeyRoute)}</span>
                <span>${escapeHtml(journeyTiming)}</span>
                <span class="fleet-transit-track" aria-label="Journey progress ${formatNumber(journeyProgress ?? 0)} percent"><i style="width: ${journeyProgress ?? 0}%"></i></span>
                <span class="fleet-journey-actions">${journeyAction}</span>
            </div>
            <div class="detail-block">
                <strong>Command availability</strong>
                <span>${pendingRecall
                    ? "Recall is queued for the next tick. Cancel it before execution to continue outward."
                    : transitCommitment.isReturning
                        ? "Unavailable until the fleet returns to its origin."
                        : "Recall is the only available change while the fleet is between systems."}</span>
            </div>
        `
        : `
            <div class="detail-block">
                <strong>Adjacent Routes</strong>
                ${linkedSystems}
            </div>
            <div class="detail-block">
                <strong>Local Fleets</strong>
                ${nearbyFleets}
            </div>
        `;

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
            ${positionRows}
            ${admiralRows}
        </dl>
        ${journeyBlock}
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
    const selectedFleetRecord = state.fleets.find(item => item.fleet.fleetId === state.selectedFleetId) ?? null;
    const selectedTransit = selectedFleetRecord?.fleet.status === "inTransit" ? selectedFleetRecord : null;
    const selectionHint = selectedTransit
        ? `${selectedTransit.fleet.fleetName} is in transit to ${selectedTransit.destinationSystemName ?? "its destination"} until T${selectedTransit.fleet.arrivalTickNumber ?? "?"}.`
        : null;
    elements.selectedFleetActionName.textContent = selectedFleetRecord?.fleet.fleetName ?? "selected fleet";

    const destinations = selectedFleet && state.fleetDetail?.fleetId === selectedFleet.fleet.fleetId
        ? state.fleetDetail.legalMoveDestinations ?? []
        : [];
    fillSelect(elements.destinationSelect, destinations, item => item.systemId, moveDestinationOptionLabel);
    if (destinations.length === 0) {
        elements.destinationSelect.innerHTML = `<option value="">No linked destinations</option>`;
    }

    const targetFactions = collectTargetFactions(selectedFleet);
    fillSelect(elements.targetEmpireSelect, targetFactions, item => item.factionId, item => item.factionName, true);
    if (targetFactions.length === 0) {
        elements.targetEmpireSelect.innerHTML = `<option value="">No local hostiles</option>`;
    }

    const fleetReady = Boolean(selectedFleet);
    const awayFromHome = fleetReady && selectedFleet.fleet.currentSystemId !== state.empire.homeSystem.systemId;
    elements.moveForm.querySelector("button[type=submit]").disabled = !fleetReady || destinations.length === 0;
    elements.attackForm.querySelector("button[type=submit]").disabled = !fleetReady || targetFactions.length === 0;
    elements.coloniseForm.querySelector("button[type=submit]").disabled = !awayFromHome;

    renderMoveActionHint();
    elements.attackActionHint.textContent = !fleetReady
        ? selectionHint ?? "Select an active fleet to prepare an attack."
        : targetFactions.length === 0
            ? "No hostile active fleet is present in this system."
            : `${formatCount(targetFactions.length, "visible local rival")} available, or choose nearest hostile.`;
    elements.coloniseActionHint.textContent = !fleetReady
        ? selectionHint ?? "Select an active fleet to assess colonisation."
        : !awayFromHome
            ? "Move this fleet beyond its home system before establishing an outpost."
            : colonisationReservationHint();
}

function moveDestinationOptionLabel(destination) {
    return `${destination.systemName} · ${formatJourneyDuration(destination.travelTicks)} · projected arrival T${formatNumber(destination.projectedArrivalTickNumber)}`;
}

function selectedMoveDestination() {
    if (!state.fleetDetail || state.fleetDetail.fleetId !== state.selectedFleetId) {
        return null;
    }

    return (state.fleetDetail.legalMoveDestinations ?? [])
        .find(item => item.systemId === elements.destinationSelect.value) ?? null;
}

function renderMoveActionHint() {
    const selectedFleet = state.fleets.find(item => item.fleet.fleetId === state.selectedFleetId) ?? null;
    if (!selectedFleet || selectedFleet.fleet.status !== "active" || selectedFleet.fleet.shipCount <= 0) {
        elements.moveActionHint.textContent = selectedFleet?.fleet.status === "inTransit"
            ? `${selectedFleet.fleet.fleetName} is in transit until T${selectedFleet.fleet.arrivalTickNumber ?? "?"}.`
            : "Select an active fleet to see available routes.";
        return;
    }

    const destinations = state.fleetDetail?.fleetId === selectedFleet.fleet.fleetId
        ? state.fleetDetail.legalMoveDestinations ?? []
        : [];
    const destination = selectedMoveDestination();
    if (!destination) {
        elements.moveActionHint.textContent = destinations.length === 0
            ? "This fleet has no linked destination available."
            : `${formatCount(destinations.length, "adjacent destination")} available from ${selectedFleet.currentSystemName}.`;
        return;
    }

    elements.moveActionHint.textContent = `${formatJourneyDuration(destination.travelTicks)} · `
        + `projected dispatch T${formatNumber(destination.projectedDispatchTickNumber)} · `
        + `projected arrival T${formatNumber(destination.projectedArrivalTickNumber)}. `
        + "The route and timing are revalidated when the command activates.";
}

function formatJourneyDuration(travelTicks) {
    return `${formatNumber(travelTicks)}-tick journey`;
}

function colonisationReservationHint() {
    const pendingColonisations = state.orders.filter(order =>
        order.status === "pending" && order.orderType === "colonise").length;
    if (pendingColonisations === 0) {
        return "Costs 100 Population and resolves next tick. Current-turn Population income counts when commands close.";
    }

    const requiredPopulation = pendingColonisations * 100;
    const currentPopulation = Number(state.empire?.resources?.population ?? 0);
    if (currentPopulation >= requiredPopulation) {
        return `${formatCount(pendingColonisations, "pending Colonise order")} reserve ${formatNumber(requiredPopulation)} Population at closure; current stockpile covers the whole set.`;
    }

    return `${formatCount(pendingColonisations, "pending Colonise order")} require ${formatNumber(requiredPopulation)} Population. `
        + `You have ${formatNumber(currentPopulation)} now; current-turn income counts at closure, but the whole set is rejected if the final budget is short.`;
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
        .map(([factionId, value]) => {
            const isOwn = factionId === state.empire.factionId;
            const faction = (state.galaxy.factions ?? []).find(item => item.factionId === factionId);
            const label = isOwn ? state.empire.empireName : faction?.factionName ?? `Rival signal ${factionId.slice(0, 5)}`;
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
    const ownPresence = Number(presence[state.empire.factionId] ?? 0);
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
    const transits = transitCommitments();
    const currentTick = state.cycle?.currentTickNumber ?? 0;
    const forecast = state.turnResolution?.forecast ?? null;
    const scheduledDeliveries = forecast?.scheduledDeliveries ?? [];
    const projectedDeliveryTick = forecast?.automaticMilitaryProgramme?.projectedDeliveryTick ?? null;
    const finalTick = Math.max(
        currentTick + 5,
        ...pendingOrders.map(order => Number(order.executeAfterTick ?? currentTick + 1)),
        ...transits.map(transitEffectiveArrivalTick),
        ...scheduledDeliveries.map(delivery => Number(delivery.deliveryTick)),
        Number(projectedDeliveryTick ?? currentTick + 1));
    const turns = Array.from({ length: finalTick - currentTick + 1 }, (_, index) => currentTick + index);
    elements.orders.innerHTML = turns.map(tick => {
        const turnOrders = pendingOrders
            .filter(order => Number(order.executeAfterTick ?? currentTick + 1) === tick)
            .sort((left, right) => left.fleetName.localeCompare(right.fleetName));
        const turnTransits = transits.filter(transit => !transit.pendingRecallOrderId && transit.arrivalTickNumber === tick);
        const projectedReturns = transits.filter(transit => transit.pendingRecallOrderId
            && transit.projectedReturnArrivalTickNumber > transit.recallExecuteTickNumber
            && transit.projectedReturnArrivalTickNumber === tick);
        const isCurrent = tick === currentTick;
        const isNext = tick === currentTick + 1;
        const commitments = [
            ...turnOrders.map(orderCalendarCard),
            ...turnTransits.map(transitCalendarCard),
            ...projectedReturns.map(projectedReturnCalendarCard),
            ...turnForecastCalendarCards(tick)
        ];
        const content = commitments.length > 0
            ? commitments.join("")
            : `<p class="calendar-empty">${isCurrent
                ? commandsAreOpen() ? "Published state · commands open" : `${escapeHtml(state.turnResolution?.stageLabel ?? "Turn resolution")} underway`
                : "No player orders or scheduled effects"}</p>`;
        return `
            <section class="calendar-turn${isCurrent ? " is-current" : ""}${isNext ? " is-next" : ""}" aria-label="Turn ${tick}">
                <header>
                    <strong>T${tick}</strong>
                    <span>${isCurrent ? "Now" : isNext ? "Next" : "Forecast"}</span>
                </header>
                <div class="calendar-turn-orders">${content}</div>
            </section>
        `;
    }).join("");

    renderOrderHistory();
}

function turnForecastCalendarCards(tick) {
    const turn = state.turnResolution;
    if (!turn) {
        return [];
    }

    const forecast = turn.forecast;
    const cards = [];
    if (tick === turn.nextTickNumber) {
        const income = forecast.expectedIncome;
        if (income.industry > 0 || income.research > 0 || income.population > 0) {
            cards.push(calendarEffectCard(
                "I",
                "Projected income",
                `+${formatNumber(income.industry)} Industry · +${formatNumber(income.research)} Research · +${formatNumber(income.population)} Population`,
                "projection"));
        }

        const reservation = forecast.colonisationReservation;
        if (reservation.orderCount > 0) {
            cards.push(calendarEffectCard(
                "O",
                "Projected Colonise reservation",
                `${formatNumber(reservation.populationRequired)} Population at closure · ${reservation.isFullyFunded ? "complete set funded" : "complete set at risk"}`,
                reservation.isFullyFunded ? "projection" : "warning"));
        }

        const programme = forecast.automaticMilitaryProgramme;
        if (programme.projectedShipCount > 0) {
            cards.push(calendarEffectCard(
                "P",
                "Projected Military programme",
                `${formatNumber(programme.projectedIndustrySpend)} Industry · starts ${formatCount(programme.projectedShipCount, "ship")}`,
                "projection"));
        }

        if (forecast.surveyProjectionExpectedNextWindow) {
            cards.push(calendarEffectCard(
                "D",
                "Projected progression",
                "Survey Projection applies when the next command window opens",
                "projection"));
        }
    }

    for (const delivery of forecast.scheduledDeliveries.filter(item => item.deliveryTick === tick)) {
        cards.push(calendarEffectCard(
            "C",
            "Committed ship delivery",
            `${formatCount(delivery.shipCount, "ship")} · ${formatNumber(delivery.industryCommitted)} Industry already committed`,
            "commitment"));
    }

    const projectedProgramme = forecast.automaticMilitaryProgramme;
    if (projectedProgramme.projectedShipCount > 0 && projectedProgramme.projectedDeliveryTick === tick) {
        cards.push(calendarEffectCard(
            "C",
            "Projected ship delivery",
            `${formatCount(projectedProgramme.projectedShipCount, "ship")} if the current programme forecast seals`,
            "projection"));
    }

    return cards;
}

function calendarEffectCard(glyph, title, detail, kind) {
    return `
        <article class="calendar-order calendar-effect is-${kind}">
            <span class="calendar-order-glyph" aria-hidden="true">${glyph}</span>
            <div>
                <strong>${escapeHtml(title)}</strong>
                <span>${escapeHtml(detail)}</span>
            </div>
        </article>
    `;
}

function transitCalendarCard(transit) {
    const returning = transit.isReturning;
    const timing = returning
        ? `Recalled T${formatTickNumber(transit.departureTickNumber)} · arrives T${formatNumber(transit.arrivalTickNumber)}`
        : `${formatJourneyDuration(transitJourneyDuration(transit) ?? 1)} · dispatched T${formatTickNumber(transit.dispatchedTickNumber)} · arrives T${formatNumber(transit.arrivalTickNumber)}`;
    return `
        <article class="calendar-order order-intransit">
            <span class="calendar-order-glyph" aria-hidden="true">${returning ? "R" : "M"}</span>
            <div>
                <strong>${returning ? "Return underway" : "Move underway"}: ${escapeHtml(transit.fleetName)}</strong>
                <span>${escapeHtml(returning ? transit.originSystemName : transit.outwardDestinationSystemName)} · ${escapeHtml(timing)}</span>
            </div>
        </article>
    `;
}

function projectedReturnCalendarCard(transit) {
    return `
        <article class="calendar-order order-intransit">
            <span class="calendar-order-glyph" aria-hidden="true">R</span>
            <div>
                <strong>Projected return: ${escapeHtml(transit.fleetName)}</strong>
                <span>${escapeHtml(transit.originSystemName)} · projected return T${formatNumber(transit.projectedReturnArrivalTickNumber)}</span>
            </div>
        </article>
    `;
}

function orderCalendarCard(order) {
    const target = order.targetSystemName ?? order.targetFactionName ?? "nearest hostile";
    const glyph = ({ moveFleet: "M", recallFleet: "R", attack: "X", colonise: "O", hold: "H" })[order.orderType] ?? "·";
    const recallTransit = order.orderType === "recallFleet"
        ? transitCommitments().find(transit => transit.pendingRecallOrderId === order.fleetOrderId) ?? null
        : null;
    const timing = recallTransit
        ? `Projected reversal T${formatNumber(order.executeAfterTick)} · projected return T${formatTickNumber(recallTransit.projectedReturnArrivalTickNumber)}`
        : order.orderType === "moveFleet"
            ? `${escapeHtml(target)} · ${escapeHtml(formatOrderTiming(order))}`
            : `${escapeHtml(target)} · Earliest T${formatNumber(order.executeAfterTick)}`;
    return `
        <article class="calendar-order order-${statusClass(order.status)}">
            <span class="calendar-order-glyph" aria-hidden="true">${glyph}</span>
            <div>
                <strong>${escapeHtml(formatOrderType(order.orderType))}: ${escapeHtml(order.fleetName)}</strong>
                <span>${timing}</span>
            </div>
            <button type="button" class="inline-action" data-cancel-order-id="${order.fleetOrderId}">Cancel</button>
        </article>
    `;
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
    const target = order.targetSystemName ?? order.targetFactionName ?? "nearest hostile";
    const timing = formatOrderTiming(order);
    const rejection = order.rejectionReason ? ` | ${order.rejectionReason}` : "";
    const replacement = order.supersededByOrderId
        ? state.orders.find(item => item.fleetOrderId === order.supersededByOrderId)
        : null;
    const replacementDetail = replacement
        ? `<span>Replaced by ${escapeHtml(formatOrderIntent(replacement))}</span>`
        : order.status === "superseded"
            ? `<span>Replacement retained outside the current history window</span>`
            : "";
    const cancelButton = allowCancel
        ? `<button type="button" class="inline-action" data-cancel-order-id="${order.fleetOrderId}">Cancel</button>`
        : "";
    const presentationStatus = transitCommitmentForOrder(order) ? "inTransit" : order.status;
    return `
        <article class="item order-${statusClass(presentationStatus)}">
            <strong>${escapeHtml(formatOrderType(order.orderType))}: ${escapeHtml(order.fleetName)}</strong>
            <span class="item-meta">
                ${statusChip(presentationStatus)}
                <span>${escapeHtml(target)}</span>
                <span>${escapeHtml(timing)}${escapeHtml(rejection)}</span>
                ${replacementDetail}
            </span>
            ${cancelButton}
        </article>
    `;
}

async function cancelOrder(fleetOrderId) {
    if (!commandsAreOpen()) {
        setMessage(`${state.turnResolution?.stageLabel ?? "This turn"} does not accept cancellations.`);
        return;
    }

    if (!state.empire) {
        setMessage("Login before cancelling orders.");
        return;
    }

    try {
        await gameApi.deleteJson(`/orders/${encodeURIComponent(fleetOrderId)}`);
        setMessage("Order cancelled.");
        await refresh();
    } catch (error) {
        if (isGameRequestCancellation(error)) {
            return;
        }

        setMessage(error.message);
    }
}

async function recallFleet(fleetId) {
    if (!commandsAreOpen()) {
        setMessage(`${state.turnResolution?.stageLabel ?? "This turn"} does not accept Recall commands.`);
        return;
    }

    if (!state.empire) {
        setMessage("Login before recalling fleets.");
        return;
    }

    const transit = transitCommitments().find(item => item.fleetId === fleetId);
    if (!transit || transit.isReturning || transit.pendingRecallOrderId) {
        setMessage("This fleet cannot be recalled from its current journey state.");
        return;
    }

    const nextTick = (state.cycle?.currentTickNumber ?? 0) + 1;
    const projectedReturnTick = transit.departureTickNumber === null
        ? nextTick
        : nextTick + Math.max(1, nextTick - transit.departureTickNumber) - 1;
    const confirmed = window.confirm(
        `Recall ${transit.fleetName} to ${transit.originSystemName}?\n\n` +
        `It will reverse course at T${nextTick} and is projected to return at T${projectedReturnTick}. ` +
        "The original move remains in history.");
    if (!confirmed) {
        return;
    }

    try {
        await gameApi.postJson("/orders/recall", { fleetId });
        setMessage("Recall order queued.");
        await refresh();
    } catch (error) {
        if (isGameRequestCancellation(error)) {
            return;
        }

        setMessage(error.message);
    }
}

function syncTutorialAfterRefresh() {
    if (isTrainingGame() && state.tutorialJourney) {
        syncTrainingTutorialAfterRefresh();
        return;
    }

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
        tutorial.dismissed = false;

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

function isTrainingGame() {
    return String(gameById(state.gameId)?.game?.purpose ?? "").toLowerCase() === "training";
}

function normaliseTutorialStatus(value) {
    return String(value ?? "available").replace(/([a-z])([A-Z])/g, "$1-$2").toLowerCase();
}

function syncTrainingTutorialAfterRefresh() {
    const journey = state.tutorialJourney;
    const storageKey = `cycles.tutorial.server.${journey.run.tutorialRunId}`;
    const status = normaliseTutorialStatus(journey.journeyStatus);
    if (tutorial.storageKey !== storageKey) {
        clearTutorialTarget();
        tutorial.storageKey = storageKey;
        tutorial.active = status === "active";
        tutorial.dismissed = false;
        tutorial.returnFocus = null;
    }

    tutorial.status = status;
    if (["paused", "skipped"].includes(status)) {
        tutorial.active = false;
    }
    updateTutorialButton();
    if (tutorial.active && !tutorial.dismissed) {
        renderTrainingTutorial();
    } else {
        elements.tutorialPanel.hidden = true;
        document.body.classList.remove("tutorial-active");
        clearTutorialTarget();
        syncTutorialPresentation();
    }
}

function renderTrainingTutorial() {
    const journey = state.tutorialJourney;
    if (!journey) {
        return;
    }

    updateTutorialButton();
    const status = normaliseTutorialStatus(journey.journeyStatus);
    const lesson = journey.currentLesson;
    const completedCount = journey.lessons.filter(item =>
        normaliseTutorialStatus(item.completionState) === "completed").length;
    const target = lesson ? trainingTutorialTarget(lesson) : null;

    clearTutorialTarget();
    renderTutorialAdmiral();
    elements.tutorialPanel.classList.toggle("is-right", tutorialPanelShouldSitOnRight({}, target));
    elements.tutorialPanel.hidden = false;
    document.body.classList.add("tutorial-active");
    elements.tutorialKicker.textContent = journey.journeyName;
    elements.tutorialProgress.textContent = journey.coreCompleted
        ? "Complete"
        : `${Math.min(completedCount + 1, journey.lessons.length)} of ${journey.lessons.length}`;
    elements.tutorialResetButton.textContent = "Start fresh";
    elements.tutorialPauseButton.hidden = status !== "active";
    elements.tutorialSkipButton.textContent = "I know this";
    elements.tutorialSkipButton.hidden = status !== "active";
    elements.tutorialBackButton.hidden = true;

    if (status === "recovery-required") {
        elements.tutorialTitle.textContent = "This attempt needs recovery";
        elements.tutorialBody.textContent = "The last authoritative resolution did not complete. No lesson progress has been invented or rolled back.";
        elements.tutorialHint.textContent = "Start a fresh Training game to continue with a clean world while preserving this attempt for diagnosis.";
        elements.tutorialRequirement.textContent = "Resolution is disabled for this attempt.";
        elements.tutorialNextButton.textContent = "Recovery required";
        elements.tutorialNextButton.disabled = true;
        elements.tutorialNextButton.hidden = false;
    } else if (journey.coreCompleted || status === "completed") {
        elements.tutorialTitle.textContent = "Core foundations complete";
        elements.tutorialBody.textContent = "Four real Training turns established movement, growth, combat evidence and one command chosen without a scripted target.";
        elements.tutorialHint.textContent = "Return to Games when you are ready to continue elsewhere, or start fresh to replay this version.";
        elements.tutorialRequirement.textContent = "Completion is stored with your account.";
        elements.tutorialNextButton.hidden = true;
    } else if (status === "skipped") {
        elements.tutorialTitle.textContent = "Core foundations skipped";
        elements.tutorialBody.textContent = "This version is recorded as already known. The Training game remains available without the journey.";
        elements.tutorialHint.textContent = "Start fresh whenever you want to replay the guided path from its original world state.";
        elements.tutorialRequirement.textContent = "Skipping does not gate standard games.";
        elements.tutorialNextButton.hidden = true;
    } else if (lesson) {
        elements.tutorialTitle.textContent = `${lesson.key} · ${lesson.title}`;
        elements.tutorialBody.textContent = lesson.objective;
        elements.tutorialHint.textContent = `Hint · ${lesson.hint}`;
        elements.tutorialRequirement.textContent = lesson.blockedReason
            ? `Blocked: ${formatStatus(lesson.blockedReason)}. Start fresh to continue without rewriting this world.`
            : lesson.mechanicalEvidence.summary;
        elements.tutorialNextButton.hidden = false;
        const acknowledgementReady = lesson.mechanicalEvidence.satisfied
            && lesson.presentationAcknowledgement.required
            && !lesson.presentationAcknowledgement.satisfied;
        elements.tutorialNextButton.textContent = acknowledgementReady
            ? "I saw this result"
            : "Resolve Training turn";
        elements.tutorialNextButton.disabled = Boolean(lesson.blockedReason)
            || (!acknowledgementReady && !journey.canResolve);
    }

    if (target) {
        applyTutorialTarget(target);
    }
    syncTutorialPresentation();
}

function trainingTutorialTarget(lesson) {
    const evidenceSatisfied = lesson.mechanicalEvidence.satisfied;
    if ((lesson.key === "T0" || lesson.key === "T2") && evidenceSatisfied) {
        activateView("history", { updateLocation: true });
        activateHistoryTab("events");
        return document.querySelector("#eventsSection");
    }

    if (lesson.key === "T1") {
        activateView("command", { updateLocation: true });
        return elements.prioritySection;
    }

    activateView("fleets", { updateLocation: true });
    activateFleetTab("command");
    if (lesson.key === "T0") {
        activateFleetAction("move");
        return trainingFleetTarget("Home Guard");
    }
    if (lesson.key === "T2") {
        activateFleetAction("attack");
        return trainingFleetTarget("Vanguard");
    }
    return elements.fleets;
}

function trainingFleetTarget(fleetName) {
    const item = state.fleets.find(candidate => candidate.fleet.fleetName === fleetName);
    return item
        ? document.querySelector(`[data-fleet-id="${item.fleet.fleetId}"]`)
        : elements.fleets;
}

async function advanceTrainingTutorial() {
    const journey = state.tutorialJourney;
    const lesson = journey?.currentLesson;
    if (!journey || !lesson || lesson.blockedReason) {
        return;
    }

    elements.tutorialNextButton.disabled = true;
    try {
        if (lesson.mechanicalEvidence.satisfied
            && lesson.presentationAcknowledgement.required
            && !lesson.presentationAcknowledgement.satisfied) {
            state.tutorialJourney = await gameApi.postJson(
                "/tutorial/acknowledgements",
                { acknowledgementKey: lesson.presentationAcknowledgement.key });
            tutorial.status = normaliseTutorialStatus(state.tutorialJourney.journeyStatus);
            renderTrainingTutorial();
            syncCommandWindowControls();
            return;
        }

        const result = await gameApi.postJson("/tutorial/resolve", {});
        state.tutorialJourney = result.journey;
        setTurnMessage(
            `Published Training T${result.tickNumber}: ${formatCount(result.ordersProcessed, "fleet intention")}, ${formatCount(result.eventsCreated, "event")}, ${formatCount(result.battlesCreated, "battle")}.`);
        await refresh();
    } catch (error) {
        if (!isGameRequestCancellation(error)) {
            setMessage(error.message);
            renderTrainingTutorial();
        }
    }
}

async function startOrResumeTutorial() {
    if (isTrainingGame() && state.tutorialJourney) {
        tutorial.returnFocus = elements.tutorialButton;
        if (normaliseTutorialStatus(state.tutorialJourney.journeyStatus) === "paused") {
            try {
                state.tutorialJourney = await gameApi.postJson("/tutorial/status", { status: "active" });
            } catch (error) {
                if (!isGameRequestCancellation(error)) {
                    setMessage(error.message);
                }
                return;
            }
        }
        tutorial.status = normaliseTutorialStatus(state.tutorialJourney.journeyStatus);
        tutorial.active = true;
        tutorial.dismissed = false;
        renderTrainingTutorial();
        syncTutorialPresentation({ moveFocus: true });
        syncCommandWindowControls();
        return;
    }

    if (!tutorial.storageKey || !state.cycle) {
        return;
    }

    tutorial.returnFocus = elements.tutorialButton;
    tutorial.dismissed = false;
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
    renderTutorial();
    syncTutorialPresentation({ moveFocus: true });
}

async function resetTutorial() {
    if (isTrainingGame() && state.tutorialJourney) {
        const confirmed = window.confirm(
            "Start a fresh Training game? This attempt will remain in your history and its world will not be rewound.");
        if (!confirmed) {
            return;
        }

        elements.tutorialResetButton.disabled = true;
        try {
            tutorial.freshRequestId ??= crypto.randomUUID();
            const replacement = await gameApi.postJson(
                "/tutorial/start-fresh",
                { requestId: tutorial.freshRequestId });
            tutorial.freshRequestId = null;
            await loadGamesHome();
            const game = gameById(replacement.gameId);
            if (!game) {
                throw new Error("The fresh Training game was created but is not yet available in the game ledger.");
            }
            window.location.hash = selectedGameHash(game, "command");
        } catch (error) {
            if (!isGameRequestCancellation(error)) {
                setMessage(error.message);
            }
        } finally {
            elements.tutorialResetButton.disabled = false;
        }
        return;
    }

    if (!tutorial.storageKey || !state.cycle) {
        return;
    }

    tutorial.status = "active";
    tutorial.active = true;
    tutorial.stepIndex = 0;
    tutorial.initialTick = state.cycle.currentTickNumber;
    tutorial.completedActions = new Set();
    tutorial.briefing = state.openingBriefing ?? tutorial.briefing;
    saveTutorialState();
    renderTutorial();
}

async function pauseTutorial() {
    if (!tutorial.active) {
        return;
    }

    if (isTrainingGame() && state.tutorialJourney) {
        try {
            state.tutorialJourney = await gameApi.postJson("/tutorial/status", { status: "paused" });
            tutorial.status = "paused";
            tutorial.active = false;
            hideTutorial();
            syncCommandWindowControls();
        } catch (error) {
            if (!isGameRequestCancellation(error)) {
                setMessage(error.message);
            }
        }
        return;
    }

    tutorial.status = "paused";
    tutorial.active = false;
    saveTutorialState();
    hideTutorial();
}

async function skipTutorial() {
    if (isTrainingGame() && state.tutorialJourney) {
        const confirmed = window.confirm(
            "Mark this Core foundations version as already known? The Training game remains playable and you can start a fresh attempt later.");
        if (!confirmed) {
            return;
        }

        try {
            state.tutorialJourney = await gameApi.postJson("/tutorial/status", { status: "skipped" });
            tutorial.status = "skipped";
            tutorial.active = false;
            hideTutorial();
            syncCommandWindowControls();
        } catch (error) {
            if (!isGameRequestCancellation(error)) {
                setMessage(error.message);
            }
        }
        return;
    }

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
    renderTutorial();
}

async function nextTutorialStep() {
    if (isTrainingGame() && state.tutorialJourney) {
        await advanceTrainingTutorial();
        return;
    }

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
    renderTutorial();
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
    if (tutorial.active && !tutorial.dismissed) {
        renderTutorial();
    } else {
        elements.tutorialPanel.hidden = true;
        document.body.classList.remove("tutorial-active");
        clearTutorialTarget();
        syncTutorialPresentation();
    }
}

function renderTutorial() {
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
    if (step.mapSystemId && state.galaxy && state.empire) {
        focusMapOnSystem(step.mapSystemId);
        renderGalaxy(state.galaxy, state.empire);
    }

    clearTutorialTarget();
    const target = step.target?.();
    renderTutorialAdmiral();
    elements.tutorialPanel.classList.toggle("is-right", tutorialPanelShouldSitOnRight(step, target));
    elements.tutorialPanel.hidden = false;
    document.body.classList.add("tutorial-active");
    elements.tutorialKicker.textContent = "Day 1 guide";
    elements.tutorialHint.textContent = "";
    elements.tutorialResetButton.textContent = "Reset guide";
    elements.tutorialPauseButton.hidden = false;
    elements.tutorialSkipButton.textContent = "Skip guide";
    elements.tutorialSkipButton.hidden = false;
    elements.tutorialBackButton.hidden = false;
    elements.tutorialNextButton.hidden = false;
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

    if (target) {
        applyTutorialTarget(target);
    }
    syncTutorialPresentation();
}

function tutorialPanelShouldSitOnRight(step, target) {
    if (step.panelPlacement) {
        return step.panelPlacement === "right";
    }
    if (!target || window.innerWidth <= 900) {
        return false;
    }

    const bounds = target.getBoundingClientRect();
    return bounds.left + (bounds.width / 2) < window.innerWidth / 2;
}

function hideTutorial() {
    tutorial.dismissed = false;
    elements.tutorialPanel.hidden = true;
    document.body.classList.remove("tutorial-active");
    clearTutorialTarget();
    updateTutorialButton();
    syncTutorialPresentation();
    (tutorial.returnFocus ?? elements.tutorialButton).focus();
    tutorial.returnFocus = null;
}

function closeTutorialPanel() {
    tutorial.dismissed = true;
    elements.tutorialPanel.hidden = true;
    document.body.classList.remove("tutorial-active");
    clearTutorialTarget();
    updateTutorialButton();
    syncTutorialPresentation();
    (tutorial.returnFocus ?? elements.tutorialButton).focus();
    tutorial.returnFocus = null;
}

function syncTutorialPresentation({ moveFocus = false } = {}) {
    const modal = tutorial.active
        && !tutorial.dismissed
        && !elements.tutorialPanel.hidden
        && window.innerWidth < 1200;
    const enteredModal = modal && !tutorial.modal;
    tutorial.modal = modal;

    elements.tutorialPanel.setAttribute("role", modal ? "dialog" : "complementary");
    if (modal) {
        elements.tutorialPanel.setAttribute("aria-modal", "true");
    } else {
        elements.tutorialPanel.removeAttribute("aria-modal");
    }
    document.body.classList.toggle("tutorial-modal", modal);
    setTutorialBackgroundInert(modal);
    updateTutorialButton();

    if (modal
        && (enteredModal || moveFocus)
        && !elements.tutorialPanel.contains(document.activeElement)) {
        focusTutorialClose();
    }
}

function focusTutorialClose() {
    elements.tutorialCloseButton.focus({ preventScroll: true });
}

function setTutorialBackgroundInert(inert) {
    if (inert) {
        if (tutorial.inertElements.length > 0) {
            return;
        }

        tutorial.inertElements = [...document.body.children]
            .filter(element => element !== elements.tutorialPanel
                && element.tagName !== "SCRIPT"
                && !element.inert);
        tutorial.inertElements.forEach(element => {
            element.inert = true;
        });
        return;
    }

    tutorial.inertElements.forEach(element => {
        element.inert = false;
    });
    tutorial.inertElements = [];
}

function containTutorialFocus(event) {
    const focusable = [...elements.tutorialPanel.querySelectorAll(
        "a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex='-1'])")]
        .filter(element => !element.hidden && element.getClientRects().length > 0);

    if (focusable.length === 0) {
        event.preventDefault();
        focusTutorialClose();
        return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement;
    if (!elements.tutorialPanel.contains(active)) {
        event.preventDefault();
        (event.shiftKey ? last : first).focus();
    } else if (event.shiftKey && active === first) {
        event.preventDefault();
        last.focus();
    } else if (!event.shiftKey && active === last) {
        event.preventDefault();
        first.focus();
    }
}

function updateTutorialButton() {
    const open = tutorial.active && !tutorial.dismissed && !elements.tutorialPanel.hidden;
    if (isTrainingGame() && state.tutorialJourney) {
        const status = normaliseTutorialStatus(state.tutorialJourney.journeyStatus);
        const label = open
            ? "Core foundations open"
            : status === "paused" ? "Resume Core foundations"
                : status === "completed" ? "Review completed Training"
                    : status === "skipped" ? "Review skipped Training"
                        : status === "recovery-required" ? "Review Training recovery"
                            : "Open Core foundations";
        elements.tutorialButton.setAttribute("aria-label", label);
        elements.tutorialButton.title = label;
        elements.tutorialButton.setAttribute("aria-expanded", String(open));
        return;
    }

    const label = open
        ? "Guide open"
        : tutorial.status === "paused" ? "Resume guide"
            : tutorial.status === "completed" || tutorial.status === "skipped" ? "Restart guide"
                : "Guide";
    elements.tutorialButton.setAttribute("aria-label", label);
    elements.tutorialButton.title = label;
    elements.tutorialButton.setAttribute("aria-expanded", String(open));
}

function applyTutorialTarget(target) {
    tutorial.target = target;
    tutorial.targetDescribedBy = target.getAttribute("aria-describedby");
    target.classList.add("tutorial-target");
    const describedBy = new Set((tutorial.targetDescribedBy ?? "").split(/\s+/).filter(Boolean));
    describedBy.add("tutorialBody");
    target.setAttribute("aria-describedby", [...describedBy].join(" "));

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    requestAnimationFrame(() => {
        if (!tutorialTargetNeedsScroll(target)) {
            return;
        }

        target.scrollIntoView({
            behavior: reducedMotion ? "auto" : "smooth",
            block: "center",
            inline: "nearest"
        });
    });
}

function tutorialTargetNeedsScroll(target) {
    const bounds = target.getBoundingClientRect();
    const viewBounds = target.closest(".app-view")?.getBoundingClientRect();
    const visibleTop = Math.max(bounds.top, viewBounds?.top ?? 0);
    let visibleBottom = Math.min(bounds.bottom, viewBounds?.bottom ?? window.innerHeight);
    if (window.innerWidth <= 900 && !elements.tutorialPanel.hidden) {
        visibleBottom = Math.min(visibleBottom, elements.tutorialPanel.getBoundingClientRect().top);
    }

    const visibleHeight = Math.max(0, visibleBottom - visibleTop);
    return visibleHeight < Math.min(bounds.height, 120);
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
    tutorial.freshRequestId = null;
    tutorial.dismissed = false;
    syncTutorialPresentation();
}

const tutorialAdmiralPortraits = Object.freeze([
    "astrolabe-gold-human-01",
    "astrolabe-gold-human-02",
    "astrolabe-gold-human-03",
    "astrolabe-gold-human-04",
    "astrolabe-gold-human-05",
    "gateway-teal-human-01",
    "gateway-teal-human-02",
    "gateway-teal-human-03",
    "gateway-teal-human-04",
    "spearpoint-crimson-human-01",
    "spearpoint-crimson-human-02",
    "spearpoint-crimson-human-03",
    "spearpoint-crimson-human-04",
    "spearpoint-crimson-human-05",
    "archive-violet-human-01",
    "archive-violet-human-02",
    "archive-violet-human-03",
    "archive-violet-human-04"
]);

function tutorialAdmiralProfile() {
    const admiral = state.fleets
        .find(item => item.fleet.empireId === state.empire?.empireId && item.admiral)
        ?.admiral;
    const admiralName = admiral?.admiralName ?? "Fleet Command";
    const displayName = admiral && !admiralName.toLocaleLowerCase().startsWith("admiral ")
        ? `Admiral ${admiralName}`
        : admiralName;
    const portraitKey = tutorialAdmiralPortraits[stableTutorialPortraitIndex(admiral?.admiralId ?? state.playerId ?? admiralName)];

    return {
        admiral,
        displayName,
        portraitKey,
        portraitPath: `assets/admirals/portraits/${portraitKey}.webp`
    };
}

function stableTutorialPortraitIndex(value) {
    const text = String(value);
    let hash = 0;
    for (let index = 0; index < text.length; index += 1) {
        hash = ((hash * 31) + text.charCodeAt(index)) >>> 0;
    }

    return hash % tutorialAdmiralPortraits.length;
}

function renderTutorialAdmiral() {
    const profile = tutorialAdmiralProfile();
    elements.tutorialAdmiralName.textContent = profile.displayName;
    elements.tutorialAdmiralRole.textContent = profile.admiral ? "Your starting admiral" : "Fleet Command";
    elements.tutorialAdmiralPortrait.src = profile.portraitPath;
    elements.tutorialAdmiralPortrait.alt = profile.admiral ? `Portrait of ${profile.displayName}` : "Fleet Command portrait";
    elements.tutorialAdmiralPortrait.dataset.portraitId = profile.portraitKey;
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
    const guideAdmiral = tutorialAdmiralProfile();
    const playerName = state.username ?? "Commander";
    const steps = [
        {
            id: "welcome",
            view: "command",
            title: "Welcome to Cycles",
            body: `Hi, ${playerName}. ${guideAdmiral.displayName} speaking. Welcome, and let's get you settled in. I'll stay with you for the opening and point out the controls as we need them.`,
            required: false
        },
        {
            id: "command-introduction",
            view: "command",
            title: "This is Command",
            body: "Command is where you review the situation, set your priorities, check pending orders, and advance the turn. There is quite a lot here; I'll introduce it when it becomes useful.",
            target: () => document.querySelector(".command-overview-grid"),
            required: false
        },
        {
            id: "map-introduction",
            view: "galaxy",
            title: "This is the Map",
            body: "The Map shows the galaxy, your reach, known fleets, routes, and places worth your attention. You can inspect and focus the strategic picture here; I'll show you the first few things now.",
            target: () => document.querySelector("#mapPanel"),
            required: false
        },
        {
            id: "map",
            view: "galaxy",
            mapSystemId: focusSystemId,
            panelPlacement: "right",
            title: curated ? "Start with the flashpoint" : "Start with your home system",
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
            title: curated ? `Secure ${tutorialSystemName(moveTargetId)}` : "Commit a movement order",
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
                title: "Establish the frontier outpost",
                body: `Select ${tutorialFleetName(colonise.fleetId)} in the roster. This guide opens Colonise; queue the outpost from that fleet. It costs 100 population and succeeds because the fleet has the leading local influence.`,
                target: () => state.selectedFleetId === colonise.fleetId
                    ? document.querySelector("#coloniseForm")
                    : document.querySelector(`[data-fleet-id="${colonise.fleetId}"]`),
                required: true,
                requirement: "Queue the highlighted outpost.",
                isSatisfied: () => tutorialOrderExists("colonise", colonise.fleetId)
            },
            {
                id: "attack",
                view: "fleets",
                fleetTab: "command",
                fleetAction: "attack",
                title: "Answer the local challenge",
                body: `Select ${tutorialFleetName(attack.fleetId)} in the roster. This guide opens Attack; choose the local Free Captains, then queue the order. Combat is deterministic from persisted facts, but victory is not scripted. The result will enter the Chronicle if it becomes important enough.`,
                target: () => state.selectedFleetId === attack.fleetId
                    ? document.querySelector("#attackForm")
                    : document.querySelector(`[data-fleet-id="${attack.fleetId}"]`),
                required: true,
                requirement: "Queue the highlighted attack.",
                isSatisfied: () => tutorialOrderExists("attack", attack.fleetId, "targetFactionId", attack.targetFactionId)
            }
        );
    }

    steps.push(
        {
            id: "phase-order",
            view: "command",
            title: "Know how the sealed turn resolves",
            body: "Income and due construction resolve before programme spending. Recall then acts before passive arrival and movement; those positions determine combat, and only surviving eligible fleets colonise. Control is recalculated, progression is prepared for the next command window, and one complete result is published. Submission time grants no initiative.",
            target: () => document.querySelector("#turnResolutionSection"),
            required: false
        },
        {
            id: "queue",
            view: "command",
            title: "Review your commitments",
            body: curated
                ? "The queue should now hold three pending player orders alongside any automatic income, programme, construction, or journey effects. You can cancel a pending intention before closure; its submission time never grants initiative."
                : "The queue separates pending player orders from projected and already-committed effects. You can cancel a pending intention before closure; its submission time never grants initiative.",
            target: () => document.querySelector("#orderQueueSection"),
            required: curated,
            requirement: curated ? "Keep exactly the three highlighted commitments ready for Day 1." : "",
            isSatisfied: () => !curated || curatedObjectiveOrdersReady()
        },
        {
            id: "advance",
            view: "command",
            title: "Close the command window",
            body: "Close command window and advance is a temporary Development operator action. It closes this current game's shared window, lets internal planners finish, seals one fleet intention per eligible fleet, and resolves every participant through the same authoritative boundary as the Worker and CLI.",
            target: () => elements.advanceTurnButton,
            required: true,
            requirement: "Close the current game's command window and publish one complete turn.",
            isSatisfied: () => state.cycle?.currentTickNumber > tutorial.initialTick
        },
        {
            id: "resolution-results",
            view: "history",
            historyTab: "events",
            title: "Trace what actually happened",
            body: curated
                ? `Your real Day One outcomes are ${tutorialObjectiveOutcomeSummary()}. Read them in authoritative phase order: income and construction first, movement before combat, combat before colonisation, then next-window progression and publication. Event timestamps do not grant priority.`
                : "Events are the factual audit trail, grouped by authoritative phase rather than submission or display time. Check which orders processed, what resources changed, and whether anything was rejected when the world changed underneath an intention.",
            target: () => document.querySelector("#eventsSection"),
            required: curated,
            requirement: curated ? "Confirm that the move, attack, and colonisation intentions have recorded real processed or rejected outcomes." : "",
            isSatisfied: () => !curated || curatedObjectiveOutcomesRecorded()
        }
    );

    steps.push({
        id: "chronicle",
        view: "history",
        historyTab: "chronicle",
        title: "See what became history",
        body: curated
            ? "The Chronicle preserves exceptional events, not every routine action. A battle appears here only when real losses, strategy, and prior history carry it across the importance threshold."
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
            targetProperty: objectives.attack.targetFactionId ? "targetFactionId" : "targetEmpireId",
            targetId: objectives.attack.targetFactionId ?? objectives.attack.targetEmpireId
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

function collectTargetFactions(selectedFleet) {
    if (!selectedFleet
        || selectedFleet.fleet.status !== "active"
        || selectedFleet.fleet.shipCount <= 0
        || !state.fleetDetail
        || state.fleetDetail.fleetId !== selectedFleet.fleet.fleetId
        || !state.galaxy) {
        return [];
    }

    const visibleHostileFactions = new Map(
        state.fleetDetail.activeFleetsInSystem
            .filter(fleet => fleet.status === "active"
                && fleet.shipCount > 0
                && fleet.factionId !== selectedFleet.fleet.factionId)
            .map(fleet => [fleet.factionId, fleet.factionName]));
    const knownFactionNames = new Map(
        (state.galaxy.factions ?? []).map(faction => [faction.factionId, faction.factionName]));

    return [...visibleHostileFactions.keys()].map(id => ({
        factionId: id,
        factionName: visibleHostileFactions.get(id) ?? knownFactionNames.get(id) ?? id.slice(0, 8)
    }));
}

function curatedObjectiveOutcomesRecorded() {
    const objectives = tutorial.briefing?.objectives;
    if (!objectives?.move || !objectives.colonise || !objectives.attack) {
        return false;
    }

    return tutorialObjectiveOrders(objectives).every(expected => state.orders.some(order =>
        order.orderType === expected.orderType
        && order.fleetId === expected.fleetId
        && (!expected.targetProperty || order[expected.targetProperty] === expected.targetId)
        && (order.status === "processed" || order.status === "rejected")
        && Number(order.processedTick ?? 0) > tutorial.initialTick));
}

function tutorialObjectiveOutcomeSummary() {
    const objectives = tutorial.briefing?.objectives;
    if (!objectives?.move || !objectives.colonise || !objectives.attack) {
        return "not yet available";
    }

    return tutorialObjectiveOrders(objectives).map(expected => {
        const order = state.orders
            .filter(candidate => candidate.orderType === expected.orderType
                && candidate.fleetId === expected.fleetId
                && (!expected.targetProperty || candidate[expected.targetProperty] === expected.targetId))
            .sort((left, right) => Number(right.processedTick ?? -1) - Number(left.processedTick ?? -1))[0];
        return `${formatOrderType(expected.orderType)} ${order ? formatStatus(order.status) : "pending"}`;
    }).join(", ");
}

function tutorialObjectiveOrders(objectives) {
    return [
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
            targetProperty: objectives.attack.targetFactionId ? "targetFactionId" : "targetEmpireId",
            targetId: objectives.attack.targetFactionId ?? objectives.attack.targetEmpireId
        }
    ];
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
            if (state.eventSort === "resolution") {
                return right.tickNumber - left.tickNumber
                    || eventResolutionPhaseOrder(left) - eventResolutionPhaseOrder(right)
                    || String(left.eventId).localeCompare(String(right.eventId));
            }

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
        : state.eventSort === "resolution"
            ? renderPhaseOrderedEvents(filteredEvents)
            : filteredEvents.map(eventCard).join("");
}

function renderPhaseOrderedEvents(events) {
    const groups = [];
    for (const event of events) {
        const phaseOrder = eventResolutionPhaseOrder(event);
        const groupKey = `${event.tickNumber}:${phaseOrder}`;
        let group = groups.at(-1);
        if (!group || group.key !== groupKey) {
            group = {
                key: groupKey,
                tickNumber: event.tickNumber,
                phaseOrder,
                hasResolutionPhase: event.resolutionPhaseOrder !== null && event.resolutionPhaseOrder !== undefined,
                phaseTitle: eventResolutionPhaseTitle(event),
                events: []
            };
            groups.push(group);
        }
        group.events.push(event);
    }

    return groups.map(group => `
        <section class="event-phase-group" aria-labelledby="event-phase-${group.tickNumber}-${group.phaseOrder}">
            <header class="event-phase-heading">
                <span>T${formatNumber(group.tickNumber)}</span>
                <strong id="event-phase-${group.tickNumber}-${group.phaseOrder}">${group.hasResolutionPhase ? `Phase ${formatNumber(group.phaseOrder)} · ` : ""}${escapeHtml(group.phaseTitle)}</strong>
            </header>
            <div class="event-phase-records">${group.events.map(eventCard).join("")}</div>
        </section>
    `).join("");
}

function eventCard(event) {
    const phaseTitle = eventResolutionPhaseTitle(event);
    return `
        <article class="item event-entry">
            <header class="history-entry-header">
                <span class="history-tick">T${event.tickNumber}</span>
                <strong>${escapeHtml(formatStatus(event.eventType))}</strong>
                <span class="event-phase-chip">${escapeHtml(phaseTitle)}</span>
                <span class="status-chip status-${statusClass(event.severity)}">${escapeHtml(formatStatus(event.severity))}</span>
            </header>
            <p>${escapeHtml(event.displayText)}</p>
        </article>
    `;
}

function eventResolutionPhaseOrder(event) {
    return Number.isFinite(Number(event.resolutionPhaseOrder))
        && event.resolutionPhaseOrder !== null
        ? Number(event.resolutionPhaseOrder)
        : 99;
}

function eventResolutionPhaseTitle(event) {
    const phase = state.turnResolution?.phases.find(item => item.phase === event.resolutionPhase);
    return phase?.title ?? "Command window / operational boundary";
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

    const range = currentMapRange();
    const activeSector = sectorsById.get(state.selectedSectorId) ?? sectors[0];
    const activeMembers = galaxy.systems.filter(system => system.sectorId === activeSector?.sectorId);
    const sectorContext = mapSectorContext(galaxy, state.selectedSectorId);
    const composition = mapComposition(galaxy);
    const sectorLayer = renderMapSectorLayer(galaxy, sectors, sectorContext, composition);
    const lines = range === "galaxy"
        ? renderAtlasGalaxyRoutes(galaxy, systems, sectorsById)
        : renderAtlasSectorRoutes(galaxy, systems, activeSector, activeMembers, composition, selectedId);

    const lensMetrics = activeMembers.map(system => mapLensMetric(
        system,
        presenceBySystem.get(system.systemId) ?? {},
        empire.empireId));
    const maximumLensMetric = Math.max(1, ...lensMetrics);

    const nodes = range === "galaxy" ? "" : activeMembers.map((system, index) => {
        const presence = presenceBySystem.get(system.systemId) ?? {};
        const ownPresence = Number(presence[empire.empireId] ?? 0);
        const activePresence = Object.values(presence).map(Number).filter(value => value > 0);
        const totalPresence = activePresence.reduce((total, value) => total + value, 0);
        const isContested = activePresence.length > 1;
        const lensMetric = lensMetrics[index];
        const lensIntensity = lensMetric / maximumLensMetric;
        const radius = state.mapLens === "overview"
            ? 12 + Math.min(8, Math.sqrt(ownPresence) * 0.7)
            : 12 + 10 * Math.sqrt(lensIntensity);
        const isSelected = system.systemId === selectedId;
        const isImportant = isSelected || system.systemId === homeId || isContested || system.historicalSignificance > 0;
        const isGateway = Boolean(system.isGateway) || sectorContext.gatewaySystemIds.has(system.systemId);
        const isActiveSector = system.sectorId === state.selectedSectorId;
        const isInComposition = composition.systemIds.has(system.systemId);
        const isLocalContext = composition.localContextSystemIds.has(system.systemId);
        const position = mapAtlasSystemPosition(system, activeSector, activeMembers);
        const labelOnLeft = position.x > mapBounds.width * 0.72;
        const labelX = position.x + (labelOnLeft ? -radius - 9 : radius + 9);
        const labelAnchor = labelOnLeft ? "end" : "start";
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
            !isInComposition && range === "local" ? "is-context-muted" : ""
        ].filter(Boolean).join(" ");

        return `
            <g class="${nodeClasses}" data-system-id="${system.systemId}" role="button" tabindex="0" aria-label="${escapeHtml(label)}" style="--lens-intensity: ${lensIntensity}">
                <title>${escapeHtml(label)}</title>
                <circle class="system-hit" cx="${position.x}" cy="${position.y}" r="${Math.max(34, radius + 14)}"></circle>
                <circle class="system-aura" cx="${position.x}" cy="${position.y}" r="${radius + 18}"></circle>
                ${ownPresence > 0 ? `<circle class="presence" cx="${position.x}" cy="${position.y}" r="${radius + 8}"></circle>` : ""}
                ${isContested ? `<circle class="contested-ring" cx="${position.x}" cy="${position.y}" r="${radius + 13}"></circle>` : ""}
                ${isGateway ? `<circle class="gateway-ring" cx="${position.x}" cy="${position.y}" r="${radius + 10}"></circle>` : ""}
                ${isSelected ? `
                    <circle class="selection-scan" cx="${position.x}" cy="${position.y}" r="${radius + 40}"></circle>
                    <circle class="selection-orbit" cx="${position.x}" cy="${position.y}" r="${radius + 24}"></circle>
                    ${mapSelectionReticle(position.x, position.y, radius + 31)}
                ` : ""}
                <circle class="${classes}" cx="${position.x}" cy="${position.y}" r="${radius}"></circle>
                <circle class="system-core" cx="${position.x}" cy="${position.y}" r="${Math.max(4, radius * 0.27)}"></circle>
                <text class="system-label${isSelected ? " selected-label" : ""}" x="${labelX}" y="${position.y + 6}" text-anchor="${labelAnchor}">${escapeHtml(system.systemName)}</text>
            </g>
        `;
    }).join("");

    elements.galaxyMap.dataset.mapLens = state.mapLens;
    elements.galaxyMap.dataset.mapRange = range;
    const atlasAsset = range === "galaxy"
        ? activeMapAtlas()?.galaxyAsset
        : mapAtlasSectorEntry(activeSector)?.asset;
    const chartBackground = atlasAsset
        ? `<image class="atlas-background" href="${escapeHtml(atlasAsset)}" x="${atlasBounds.x}" y="${atlasBounds.y}" width="${atlasBounds.width}" height="${atlasBounds.height}" preserveAspectRatio="xMidYMid slice"></image>`
        : `<g class="chart-starfield" aria-hidden="true">${renderMapStarfield()}</g>`;
    elements.galaxyMap.innerHTML = `
        ${chartBackground}
        <rect class="atlas-vignette" x="${atlasBounds.x}" y="${atlasBounds.y}" width="${atlasBounds.width}" height="${atlasBounds.height}"></rect>
        ${range === "local" ? `<rect class="atlas-local-wash" x="${atlasBounds.x}" y="${atlasBounds.y}" width="${atlasBounds.width}" height="${atlasBounds.height}"></rect>` : ""}
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
    renderMapRecoveryState(galaxy, presenceBySystem);
    renderSystemsAndRoutes(galaxy, empire, presenceBySystem);
}

function renderSystemsAndRoutes(galaxy, empire, presenceBySystem) {
    const sectors = normaliseGalaxySectors(galaxy)
        .slice()
        .sort((left, right) => left.sortOrder - right.sortOrder || left.sectorName.localeCompare(right.sectorName));
    const sectorsById = new Map(sectors.map(sector => [sector.sectorId, sector]));
    const systemsById = new Map(galaxy.systems.map(system => [system.systemId, system]));
    const systemsBySector = groupSystemsBySector(galaxy.systems);

    elements.systemsRoutesSummary.textContent = `${formatCount(galaxy.systems.length, "system")} · ${formatCount(galaxy.links.length, "route")}`;
    elements.systemsRoutesList.innerHTML = sectors.map(sector => {
        const sectorSystems = (systemsBySector.get(sector.sectorId) ?? [])
            .slice()
            .sort((left, right) => left.systemName.localeCompare(right.systemName));
        const containsSelection = sectorSystems.some(system => system.systemId === state.selectedSystemId);
        const systemRows = sectorSystems.map(system => {
            const selected = system.systemId === state.selectedSystemId;
            const localFleetCount = state.fleets.filter(item =>
                item.fleet.currentSystemId === system.systemId
                && item.fleet.status === "active"
                && Number(item.fleet.shipCount) > 0).length;
            const routes = topologyRoutesForSystem(galaxy, systemsById, system.systemId);
            const routeButtons = routes.map(route => {
                const destinationSector = sectorsById.get(route.destination.sectorId);
                const crossesSector = route.destination.sectorId !== system.sectorId;
                const sectorNote = crossesSector && destinationSector
                    ? ` · ${mapSectorDisplayName(destinationSector)}`
                    : "";
                const routeLabel = `${route.destination.systemName}, ${formatCount(route.travelTicks, "tick")} from ${system.systemName}${sectorNote}`;
                return `
                    <button type="button"
                            class="topology-route"
                            data-topology-destination-id="${escapeHtml(route.destination.systemId)}"
                            aria-label="Inspect ${escapeHtml(routeLabel)}">
                        <span>${escapeHtml(route.destination.systemName)}</span>
                        <small>${formatCount(route.travelTicks, "tick")}${escapeHtml(sectorNote)}</small>
                    </button>
                `;
            }).join("");
            const knownOwnership = topologyKnownOwnership(galaxy, empire, presenceBySystem, system.systemId);

            return `
                <li class="topology-system${selected ? " is-selected" : ""}" data-topology-record-id="${escapeHtml(system.systemId)}">
                    <button type="button"
                            class="topology-system-select"
                            data-topology-system-id="${escapeHtml(system.systemId)}"
                            ${selected ? "aria-current=\"location\"" : ""}>
                        <span>${escapeHtml(system.systemName)}</span>
                        <small>${escapeHtml(mapSectorDisplayName(sector))} · Known ownership: ${escapeHtml(knownOwnership)}</small>
                    </button>
                    <dl class="topology-system-facts">
                        <div><dt>Your fleets</dt><dd>${formatNumber(localFleetCount)}</dd></div>
                        <div><dt>Routes</dt><dd>${formatNumber(routes.length)}</dd></div>
                    </dl>
                    <div class="topology-routes" aria-label="Adjacent destinations from ${escapeHtml(system.systemName)}">
                        ${routeButtons || `<span class="system-empty-note">No adjacent routes.</span>`}
                    </div>
                </li>
            `;
        }).join("");

        return `
            <li class="topology-sector">
                <details ${containsSelection ? "open" : ""}>
                    <summary>
                        <span>${escapeHtml(mapSectorDisplayName(sector))}</span>
                        <small>${formatCount(sectorSystems.length, "system")}</small>
                    </summary>
                    <ol class="topology-sector-systems">${systemRows}</ol>
                </details>
            </li>
        `;
    }).join("");
}

function topologyRoutesForSystem(galaxy, systemsById, systemId) {
    return galaxy.links
        .filter(link => link.systemAId === systemId || link.systemBId === systemId)
        .map(link => ({
            destination: systemsById.get(link.systemAId === systemId ? link.systemBId : link.systemAId),
            travelTicks: Number(link.travelTicks)
        }))
        .filter(route => route.destination)
        .sort((left, right) => left.destination.systemName.localeCompare(right.destination.systemName));
}

function topologyKnownOwnership(galaxy, empire, presenceBySystem, systemId) {
    const visiblePresence = Object.entries(presenceBySystem.get(systemId) ?? {})
        .map(([factionId, value]) => [factionId, Number(value)])
        .filter(([, value]) => value > 0)
        .sort((left, right) => right[1] - left[1] || left[0].localeCompare(right[0]));
    if (visiblePresence.length === 0) {
        return "Unclaimed or unknown";
    }

    const strongestPresence = visiblePresence[0][1];
    const leaders = visiblePresence
        .filter(([, value]) => value === strongestPresence)
        .map(([factionId]) => topologyFactionName(galaxy, empire, factionId));
    return leaders.length === 1 ? leaders[0] : `Contested: ${leaders.join(" / ")}`;
}

function topologyFactionName(galaxy, empire, factionId) {
    if (factionId === empire.factionId) {
        return empire.empireName;
    }

    return (galaxy.factions ?? []).find(faction => faction.factionId === factionId)?.factionName
        ?? `Unknown signal ${factionId.slice(0, 5)}`;
}

function renderAtlasGalaxyRoutes(galaxy, systems, sectorsById) {
    const linkedSectorPairs = new Map();
    for (const link of galaxy.links) {
        const first = systems.get(link.systemAId);
        const second = systems.get(link.systemBId);
        const firstSector = sectorsById.get(first?.sectorId);
        const secondSector = sectorsById.get(second?.sectorId);
        if (firstSector && secondSector && firstSector.sectorId !== secondSector.sectorId) {
            linkedSectorPairs.set(
                mapAtlasRouteKey(firstSector.sectorName, secondSector.sectorName),
                { firstSector, secondSector });
        }
    }

    const authoredRoutes = new Map((activeMapAtlas()?.galaxyRoutes ?? [])
        .map(([firstName, secondName, path]) => [mapAtlasRouteKey(firstName, secondName), path]));
    return [...linkedSectorPairs.entries()].map(([routeKey, { firstSector, secondSector }]) => {
        const path = authoredRoutes.get(routeKey) ?? mapAtlasGalaxyRoutePath(firstSector, secondSector);
        const isContextRoute = firstSector?.sectorId === state.selectedSectorId || secondSector?.sectorId === state.selectedSectorId;
        return `
            <g class="atlas-route-overlay is-bridge${isContextRoute ? " is-selected-sector-route" : ""}">
                <path class="route-glow" d="${path}"></path>
                <path class="link" d="${path}"></path>
            </g>
        `;
    }).join("");
}

function renderAtlasSectorRoutes(galaxy, systems, sector, members, composition, selectedId) {
    if (!sector) {
        return "";
    }

    return galaxy.links.map(link => {
        const first = systems.get(link.systemAId);
        const second = systems.get(link.systemBId);
        if (!first || !second || first.sectorId !== sector.sectorId || second.sectorId !== sector.sectorId) {
            return "";
        }

        const firstPosition = mapAtlasSystemPosition(first, sector, members);
        const secondPosition = mapAtlasSystemPosition(second, sector, members);
        const path = mapAtlasSectorRoutePath(sector, first.systemName, second.systemName, firstPosition, secondPosition);
        if (!path) {
            return "";
        }

        const isSelectedRoute = first.systemId === selectedId || second.systemId === selectedId;
        const isInComposition = composition.linkIds.has(mapLinkKey(link));
        return `
            <g class="route-segment is-local-route is-active-sector${isInComposition ? " is-in-composition" : ""}${isSelectedRoute ? " is-selected" : ""}">
                <path class="route-glow" d="${path}"></path>
                <path class="link${isSelectedRoute ? " selected-route" : ""}" d="${path}"></path>
            </g>
        `;
    }).join("");
}

function mapAtlasRouteKey(first, second) {
    return [first, second].sort().join("\n");
}

function mapAtlasSectorRoutePath(sector, firstName, secondName, firstPosition, secondPosition) {
    const trace = mapAtlasSectorEntry(sector)?.routes?.find(route =>
        mapAtlasRouteKey(route[0], route[1]) === mapAtlasRouteKey(firstName, secondName));
    if (!trace) {
        return `M ${firstPosition.x} ${firstPosition.y} L ${secondPosition.x} ${secondPosition.y}`;
    }

    if (typeof trace[2] === "string") {
        return trace[2];
    }

    const control = trace[2];
    return `M ${firstPosition.x} ${firstPosition.y} Q ${control[0]} ${control[1]} ${secondPosition.x} ${secondPosition.y}`;
}

function mapAtlasGalaxyRoutePath(firstSector, secondSector) {
    const first = mapAtlasSectorPosition(firstSector);
    const second = mapAtlasSectorPosition(secondSector);
    const middleX = (first.x + second.x) / 2;
    return `M ${first.x} ${first.y} C ${middleX} ${first.y} ${middleX} ${second.y} ${second.x} ${second.y}`;
}

function activeMapAtlas() {
    return mapAtlasesByProfileKey[state.cycle?.mapProfileKey] ?? null;
}

function mapAtlasSectorEntry(sector) {
    return sector ? activeMapAtlas()?.sectors?.[sector.sectorName] ?? null : null;
}

function mapAtlasSectorPosition(sector) {
    const position = mapAtlasSectorEntry(sector)?.galaxy;
    return position
        ? { x: position[0], y: position[1] }
        : { x: Number(sector?.centreX ?? 500) * mapBounds.width / 1000, y: Number(sector?.centreY ?? 350) * mapBounds.height / 700 };
}

function mapAtlasSystemPosition(system, sector, members) {
    const position = mapAtlasSectorEntry(sector)?.systems?.[system.systemName];
    if (position) {
        return { x: position[0], y: position[1] };
    }

    const minX = Math.min(...members.map(member => Number(member.x)));
    const maxX = Math.max(...members.map(member => Number(member.x)));
    const minY = Math.min(...members.map(member => Number(member.y)));
    const maxY = Math.max(...members.map(member => Number(member.y)));
    return {
        x: 250 + (Number(system.x) - minX) / Math.max(1, maxX - minX) * (mapBounds.width - 500),
        y: 170 + (Number(system.y) - minY) / Math.max(1, maxY - minY) * (mapBounds.height - 340)
    };
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
        sectorName: groupedSystems.size === 1 ? "Known Space" : `Region ${index + 1}`,
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
    return sector.sectorName;
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
    if (currentMapRange() !== "galaxy") {
        const sector = sectors.find(candidate => candidate.sectorId === state.selectedSectorId) ?? sectors[0];
        const count = Number(sector?.systemCount ?? systemsBySector.get(sector?.sectorId)?.length ?? 0);
        const selected = galaxy.systems.find(system => system.systemId === state.selectedSystemId);
        const contextLabel = currentMapRange() === "local" && selected
            ? `Local routes from ${selected.systemName}`
            : `${formatCount(count, "system")} · ${context.adjacentSectorIds.size} neighbouring sectors`;
        return sector ? `
            <g class="atlas-sector-caption" aria-hidden="true">
                <text class="atlas-sector-kicker" x="72" y="74">${currentMapRange() === "local" ? "LOCAL CHART" : "SECTOR CHART"}</text>
                <text class="atlas-sector-title" x="72" y="118">${escapeHtml(mapSectorDisplayName(sector))}</text>
                <text class="atlas-sector-context" x="74" y="151">${escapeHtml(contextLabel)}</text>
            </g>
        ` : "";
    }

    return sectors.map(sector => {
        const members = systemsBySector.get(sector.sectorId) ?? [];
        const isActive = sector.sectorId === state.selectedSectorId;
        const isAdjacent = context.adjacentSectorIds.has(sector.sectorId);
        const isInComposition = composition.sectorIds.has(sector.sectorId);
        const position = mapAtlasSectorPosition(sector);
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
                <path class="sector-hit" d="${mapAtlasSectorContour(sector)}"></path>
                <path class="sector-lens" d="${mapAtlasSectorContour(sector)}"></path>
                <path class="sector-focus" d="${mapAtlasSectorContour(sector)}"></path>
                <circle class="sector-anchor" cx="${position.x}" cy="${position.y}" r="5"></circle>
                <text class="sector-name" x="${position.x}" y="${position.y + 104}">${escapeHtml(displayName)}</text>
                <text class="sector-count" x="${position.x}" y="${position.y + 129}">${formatCount(count, "system")}</text>
            </g>
        `;
    }).join("");
}

function mapAtlasSectorContour(sector) {
    const contour = mapAtlasSectorEntry(sector)?.galaxyContour;
    if (contour) {
        return contour;
    }

    const position = mapAtlasSectorPosition(sector);
    return `M ${position.x - 112} ${position.y} A 112 78 0 1 0 ${position.x + 112} ${position.y} A 112 78 0 1 0 ${position.x - 112} ${position.y} Z`;
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

function selectSystem(systemId, { focusMap = false, restoreMapFocus = false, restoreTopologyFocus = false } = {}) {
    const system = state.galaxy?.systems.find(candidate => candidate.systemId === systemId);
    if (!system) {
        return;
    }

    state.selectedSystemId = systemId;
    state.selectedSectorId = system.sectorId ?? state.selectedSectorId;
    if (focusMap) {
        focusMapOnSystem(systemId);
    }
    renderSystemDetails();
    renderGalaxy(state.galaxy, state.empire);
    if (restoreMapFocus) {
        focusRenderedMapNode(".system-node", "systemId", systemId);
    }
    if (restoreTopologyFocus) {
        requestAnimationFrame(() => focusTopologySystem(systemId));
    }
    syncTutorialDisplay();
}

function focusTopologySystem(systemId) {
    const button = [...elements.systemsRoutesList.querySelectorAll("[data-topology-system-id]")]
        .find(candidate => candidate.dataset.topologySystemId === systemId);
    button?.focus({ preventScroll: true });
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
    if (!mapRanges.has(range)) {
        return;
    }

    state.mapPreset = range;
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
}

function focusMapOnSector(sectorId, { recenter = true, restoreMapFocus = false } = {}) {
    if (!normaliseGalaxySectors(state.galaxy).some(sector => sector.sectorId === sectorId)) {
        return;
    }

    selectSectorRepresentative(sectorId);
    if (recenter) {
        state.mapPreset = "sector";
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
        }
    }
}

function focusRenderedMapNode(selector, dataName, value) {
    const node = [...elements.galaxyMap.querySelectorAll(selector)]
        .find(candidate => candidate.dataset[dataName] === value && candidate.getAttribute("aria-hidden") !== "true");
    (node ?? elements.galaxyMap).focus({ preventScroll: true });
}

function recoverMapToSystem(systemId) {
    if (!systemId || !state.galaxy?.systems.some(system => system.systemId === systemId)) {
        return;
    }

    state.mapPreset = "sector";
    if (state.selectedSystemId !== systemId) {
        selectSystem(systemId);
    } else {
        renderGalaxy(state.galaxy, state.empire);
    }
    const system = state.galaxy.systems.find(candidate => candidate.systemId === systemId);
    state.selectedSectorId = system?.sectorId ?? state.selectedSectorId;
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

function applyMapViewBox() {
    elements.galaxyMap.setAttribute("viewBox", `${atlasBounds.x} ${atlasBounds.y} ${atlasBounds.width} ${atlasBounds.height}`);
    elements.galaxyMap.setAttribute("preserveAspectRatio", "xMidYMid slice");
    elements.galaxyMap.dataset.mapRange = currentMapRange();
    syncMapSemanticFocus();
    for (const button of elements.mapPresetButtons) {
        button.setAttribute("aria-pressed", String(button.dataset.mapPreset === state.mapPreset));
    }
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
        await refresh();
    } catch (error) {
        if (isGameRequestCancellation(error)) {
            return;
        }

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

function selectCommandMoveTarget(targetSystemId) {
    const targetIsAvailable = Boolean(targetSystemId)
        && [...elements.destinationSelect.options].some(option => option.value === targetSystemId);
    elements.destinationSelect.value = targetIsAvailable ? targetSystemId : "";
    renderMoveActionHint();
    if (!targetIsAvailable) {
        setMessage("The briefing destination is no longer available from this fleet's current system.");
    }
    return targetIsAvailable;
}

function formatOrderType(value) {
    return String(value)
        .replace("moveFleet", "Move")
        .replace("recallFleet", "Recall")
        .replace("hold", "Hold")
        .replace("attack", "Attack")
        .replace("colonise", "Colonise");
}

function formatOrderTiming(order) {
    const transit = transitCommitmentForOrder(order);
    if (transit) {
        return order.orderType === "recallFleet"
            ? `recalled T${order.processedTick} · returns T${transit.arrivalTickNumber}`
            : `${formatJourneyDuration(transitJourneyDuration(transit) ?? 1)} · dispatched T${order.processedTick} · arrives T${transit.arrivalTickNumber}`;
    }

    if (order.status === "pending") {
        if (order.orderType === "moveFleet") {
            const projection = order.moveJourneyProjection;
            return projection?.routeAvailable
                ? `${formatJourneyDuration(projection.travelTicks)} · projected dispatch T${projection.dispatchTickNumber} · projected arrival T${projection.arrivalTickNumber}`
                : `activates T${projection?.activationTickNumber ?? order.executeAfterTick} · current route unavailable; dispatch and arrival will be revalidated`;
        }

        return `executes after T${order.executeAfterTick}`;
    }

    if (order.status === "cancelled") {
        return order.processedTick === null ? "cancelled" : `cancelled T${order.processedTick}`;
    }

    if (order.status === "superseded") {
        return order.processedTick === null ? "superseded" : `superseded T${order.processedTick}`;
    }

    return order.processedTick === null ? "processed" : `processed T${order.processedTick}`;
}

function formatAdmiral(admiral) {
    return `${admiral.admiralName} (${formatNumber(admiral.reputationScore)} rep, ${formatStatus(admiral.status)})`;
}

function createGameApi() {
    let selectedGameId = null;
    let generation = 0;
    let controller = null;

    function selectGame(gameId) {
        const normalisedGameId = String(gameId ?? "").trim().toLowerCase();
        if (!gameIdPattern.test(normalisedGameId)
            || normalisedGameId === "00000000-0000-0000-0000-000000000000") {
            throw new Error("The server did not identify a valid Game.");
        }

        if (selectedGameId === normalisedGameId && controller) {
            return { gameId: selectedGameId, generation, changed: false };
        }

        controller?.abort();
        selectedGameId = normalisedGameId;
        generation += 1;
        controller = new AbortController();
        return { gameId: selectedGameId, generation, changed: true };
    }

    function clearGame() {
        controller?.abort();
        selectedGameId = null;
        generation += 1;
        controller = null;
    }

    function captureRequest() {
        if (!selectedGameId || !controller) {
            throw new Error("A Game must be selected before making a gameplay request.");
        }

        return {
            gameId: selectedGameId,
            generation,
            signal: controller.signal
        };
    }

    function isCurrent(request) {
        return selectedGameId === request.gameId
            && generation === request.generation
            && controller?.signal === request.signal
            && !request.signal.aborted;
    }

    async function requestJson(path, options = {}) {
        if (typeof path !== "string" || !path.startsWith("/") || path.startsWith("//")) {
            throw new Error("Game API paths must be root-relative paths within the selected Game.");
        }

        const request = captureRequest();
        try {
            const result = await requestJsonCore(
                `/games/${encodeURIComponent(request.gameId)}${path}`,
                { ...options, signal: request.signal });
            if (!isCurrent(request)) {
                throw createGameRequestCancellation();
            }

            return result;
        } catch (error) {
            if (!isCurrent(request) || isGameRequestCancellation(error)) {
                throw createGameRequestCancellation();
            }

            throw error;
        }
    }

    return Object.freeze({
        selectGame,
        clearGame,
        getJson: path => requestJson(path),
        postJson: (path, body) => requestJson(path, { method: "POST", body }),
        putJson: (path, body) => requestJson(path, { method: "PUT", body }),
        deleteJson: path => requestJson(path, { method: "DELETE" })
    });
}

function createGameRequestCancellation() {
    return new DOMException("The selected Game changed before the request completed.", "AbortError");
}

function isGameRequestCancellation(error) {
    return error?.name === "AbortError";
}

function clearAntiforgeryToken() {
    antiforgeryRequestToken = null;
    antiforgeryTokenPromise = null;
    antiforgeryReady = false;
}

async function requireAntiforgeryToken() {
    if (antiforgeryRequestToken) {
        return antiforgeryRequestToken;
    }

    antiforgeryTokenPromise ??= requestJsonCore(antiforgeryEndpoint)
        .then(payload => {
            if (!payload?.requestToken) {
                throw new Error("The server did not issue a security token.");
            }

            antiforgeryRequestToken = payload.requestToken;
            antiforgeryReady = true;
            return antiforgeryRequestToken;
        })
        .finally(() => {
            antiforgeryTokenPromise = null;
        });
    return antiforgeryTokenPromise;
}

async function getJson(url, options = {}) {
    return requestJsonCore(url, options);
}

async function postJson(url, body, options = {}) {
    return requestJsonCore(url, { ...options, method: "POST", body });
}

async function requestJsonCore(url, { method = "GET", body, signal } = {}) {
    const normalisedMethod = method.toUpperCase();
    const stateChanging = !["GET", "HEAD", "OPTIONS"].includes(normalisedMethod);
    const headers = { Accept: "application/json" };
    if (body !== undefined) {
        headers["Content-Type"] = "application/json";
    }

    if (stateChanging) {
        headers[antiforgeryHeaderName] = await requireAntiforgeryToken();
    }

    const response = await fetch(url, {
        method: normalisedMethod,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        credentials: "same-origin",
        signal
    });

    try {
        return await readResponse(response);
    } catch (error) {
        if (stateChanging && error.code === antiforgeryErrorCode) {
            clearAntiforgeryToken();
            try {
                await requireAntiforgeryToken();
            } catch {
                // Keep mutation controls disabled. The original request is never replayed.
            }
        }

        throw error;
    }
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
        input.disabled = state.prioritySaving || isInactive || !commandsAreOpen();
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
    elements.prioritySaveButton.disabled = !isDirty || total !== 100 || state.prioritySaving || !commandsAreOpen();
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

function formatTickNumber(value) {
    return value === null || value === undefined ? "?" : formatNumber(value);
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
        Number((presence.get(system.systemId) ?? {})[empire.factionId] ?? 0) > 0);
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
