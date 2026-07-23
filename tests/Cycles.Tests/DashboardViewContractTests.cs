using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class DashboardViewContractTests
{
    [Fact]
    public void Dashboard_groups_player_work_into_four_addressable_views()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Equal(4, Regex.Matches(html, "data-view-link=").Count);
        Assert.Equal(4, Regex.Matches(html, "class=\"app-view").Count);
        Assert.Contains("href=\"#command\"", html);
        Assert.Contains("href=\"#galaxy\"", html);
        Assert.Contains("href=\"#fleets\"", html);
        Assert.Contains("href=\"#history\"", html);
        Assert.DoesNotContain("class=\"side-panel\"", html);
        Assert.Contains("id=\"appHeaderControls\" class=\"app-header-controls\"", html);
        Assert.DoesNotContain("class=\"toolbar\"", html);
        Assert.DoesNotContain("class=\"view-heading", html);
        Assert.DoesNotContain("class=\"panel-heading\"", html);
        Assert.DoesNotContain("id=\"commandPulse\"", html);
        Assert.DoesNotContain("renderCommandSummary", script);
        Assert.Equal(4, Regex.Matches(html, "<h1[^>]*class=\"workspace-title[^\"]*\"[^>]*tabindex=\"-1\"").Count);
        Assert.DoesNotMatch(new Regex("<h1[^>]*class=\"visually-hidden\""), html);
        Assert.Contains("requestAnimationFrame(() => heading?.focus());", script);
        Assert.Contains("requestAnimationFrame(() => elements.gamesHomeTitle.focus());", script);
        Assert.DoesNotContain("heading?.focus({ preventScroll: true })", script);

        Assert.Contains("window.addEventListener(\"hashchange\"", script);
        Assert.Contains("const selectedHash = selectedGameHash(selectedGame, selectedView);", script);
        Assert.Contains("window.history.replaceState(null, \"\", selectedHash);", script);
        Assert.Contains("link.setAttribute(\"aria-current\", \"page\");", script);
    }

    [Fact]
    public void Parent_shell_presents_cycle_state_and_authored_workspace_navigation()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("class=\"brand-orbit\"", html);
        Assert.Contains("id=\"nextTurnStatus\"", html);
        Assert.DoesNotContain("class=\"view-nav-chapter\"", html);
        Assert.Contains("Triage the Cycle", html);
        Assert.Contains("Read the frontier", html);
        Assert.Contains("Commit intentions", html);
        Assert.Contains("Consult the record", html);
        Assert.Contains("elements.nextTurnStatus.textContent", script);
    }

    [Fact]
    public void Header_actions_use_square_icon_buttons_with_accessible_names()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");
        var toolbarStart = html.IndexOf("<div class=\"toolbar-actions\">", StringComparison.Ordinal);
        var toolbarEnd = html.IndexOf("</div>", toolbarStart, StringComparison.Ordinal);
        var toolbar = html[toolbarStart..toolbarEnd];

        Assert.Equal(3, Regex.Matches(toolbar, "class=\"toolbar-icon-button\"").Count);
        Assert.Contains("styles.css?v=20260721-command-focus-2", html);
        Assert.Contains("app.js?v=20260723-oidc-authority", html);
        Assert.Contains("aria-label=\"Core foundations\"", toolbar);
        Assert.Contains("aria-label=\"Close command window and advance\"", toolbar);
        Assert.Contains("aria-label=\"Refresh\"", toolbar);
        Assert.DoesNotContain(">Guide</button>", toolbar);
        Assert.DoesNotContain(">Advance turn</button>", toolbar);
        Assert.DoesNotContain(">Refresh</button>", toolbar);
        Assert.Matches(
            new Regex(@"\.toolbar-actions \.toolbar-icon-button\s*\{[^}]*inline-size:\s*44px;[^}]*block-size:\s*44px;", RegexOptions.Singleline),
            css);
        Assert.Matches(
            new Regex(@"\.workspace-title:focus\s*\{[^}]*outline:\s*2px solid var\(--gold\);", RegexOptions.Singleline),
            css);
        Assert.Contains(".toolbar-actions .toolbar-icon-button[hidden]", css);
        Assert.Contains("assets/icons/guide.svg", css);
        Assert.Contains("assets/icons/advance-turn.svg", css);
        Assert.Contains("assets/icons/refresh.svg", css);
        Assert.Contains("elements.tutorialButton.setAttribute(\"aria-label\", label);", script);

        Assert.Contains("viewBox=\"0 0 24 24\"", ReadDashboardAsset(Path.Combine("assets", "icons", "guide.svg")));
        Assert.Contains("viewBox=\"0 0 24 24\"", ReadDashboardAsset(Path.Combine("assets", "icons", "advance-turn.svg")));
        Assert.Contains("viewBox=\"0 0 24 24\"", ReadDashboardAsset(Path.Combine("assets", "icons", "refresh.svg")));
    }

    [Fact]
    public void Dashboard_presents_a_fixed_150_turn_progress_ribbon()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("id=\"turnProgressRibbon\" class=\"turn-progress-ribbon\"", html);
        Assert.Contains("id=\"turnProgressStatus\">T0 / 150", html);
        Assert.Contains("id=\"turnProgressTrack\"", html);
        Assert.Contains("role=\"progressbar\"", html);
        Assert.Contains("aria-valuemax=\"150\"", html);
        Assert.Equal(7, Regex.Matches(html, @"<span>T(?:0|25|50|75|100|125|150)</span>").Count);

        Assert.Contains("const cycleTurnLimit = 150;", script);
        Assert.Contains("renderTurnTimeline(cycle.currentTickNumber);", script);
        Assert.Contains("elements.turnProgressTrack.style.setProperty(\"--turn-progress\"", script);
        Assert.Contains("document.body.classList.add(\"dashboard-active\");", script);
        Assert.Contains("document.body.classList.remove(\"dashboard-active\");", script);
        Assert.Contains("document.body.classList.toggle(\"turn-ribbon-active\", !selfPaced);", script);

        Assert.Matches(
            new Regex(@"\.turn-progress-ribbon\s*\{[^}]*position:\s*fixed;[^}]*bottom:\s*0;", RegexOptions.Singleline),
            css);
        Assert.Contains("background-size: 0.6666667% 100%;", css);
        Assert.Contains("body.turn-ribbon-active", css);
        Assert.Contains("padding-bottom: var(--turn-ribbon-height);", css);
    }

    [Fact]
    public void Dashboard_requests_web_delivery_artwork_instead_of_png_masters()
    {
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("/assets/galaxy/galaxy-overview.webp", script);
        Assert.Equal(8, Regex.Matches(script, @"/assets/galaxy/sector-[a-z-]+\.webp").Count);
        Assert.Equal(3, Regex.Matches(script, @"/assets/galaxy/twin-reaches-[a-z-]+\.webp").Count);
        Assert.Equal(4, Regex.Matches(css, @"navigation-backgrounds/letterbox/[a-z-]+\.webp").Count);
        Assert.Equal(3, Regex.Matches(css, @"resource-backgrounds/[a-z-]+\.webp").Count);
        Assert.Contains("navigation-backgrounds/command.webp", css);
        Assert.DoesNotContain(".png", script);
        Assert.DoesNotContain(".png", css);
    }

    [Fact]
    public void Galaxy_view_keeps_the_chart_anchored_and_supports_strategic_exploration()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("id=\"systemSearchForm\"", html);
        Assert.Equal(5, Regex.Matches(html, "data-map-lens=").Count);
        Assert.Contains("id=\"mapToolbar\"", html);
        Assert.Contains("id=\"mapRangeSwitch\"", html);
        Assert.Contains("id=\"mapOwnershipStats\"", html);
        Assert.DoesNotContain("id=\"mapZoomOut\"", html);
        Assert.DoesNotContain("id=\"mapResetView\"", html);
        Assert.DoesNotContain("id=\"mapZoomIn\"", html);
        Assert.DoesNotContain("id=\"mapStats\"", html);
        Assert.DoesNotContain("class=\"map-legend\"", html);
        Assert.Contains("aria-describedby=\"mapInteractionHint\"", html);

        Assert.DoesNotContain("elements.galaxyMap.addEventListener(\"wheel\"", script);
        Assert.DoesNotContain("elements.galaxyMap.addEventListener(\"pointerdown\"", script);
        Assert.DoesNotContain("function zoomMap", script);
        Assert.Contains("function setMapRange", script);
        Assert.Contains("function mapComposition", script);
        Assert.Contains("mapAtlasesByProfileKey", script);
        Assert.Contains("viewBox=\"-407 0 2400 992\"", html);
        Assert.Contains("preserveAspectRatio=\"xMidYMid slice\"", html);
        Assert.Contains("function renderMapOwnershipStats", script);
        Assert.Contains("function renderMapInsight", script);
        Assert.Contains("selected-route", script);
        Assert.Contains("data-focus-system", script);
        Assert.Contains("data-command-fleet", script);

        Assert.Contains("grid-template-rows: auto minmax(0, 1fr);", css);
        Assert.Equal(
            2,
            Regex.Matches(css, @"grid-template-rows:\s*auto auto auto minmax\(0, 1fr\);").Count);
        Assert.Contains("body.tutorial-active .app-shell", css);
        Assert.Contains("body.tutorial-active .tutorial-panel", css);
        Assert.Contains(".map-panel {", css);
        Assert.Contains("position: sticky;", css);
    }

    [Fact]
    public void Command_view_prioritises_decisions_forecast_and_commitments_without_a_parallel_map()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("id=\"orderQueueSection\"", html);
        Assert.Contains("id=\"orderHistory\"", html);
        Assert.Contains("id=\"fleetHistoryPanel\"", html);
        Assert.Contains("id=\"orderHistoryScope\"", html);
        Assert.Contains("id=\"orderHistoryStatus\"", html);
        Assert.Contains("orderHistoryLimit: 20", script);
        Assert.Contains("orders.filter(order => order.status === \"pending\")", script);
        Assert.Contains("state.orders.filter(order => order.status !== \"pending\")", script);
        Assert.Contains("state.orderHistoryLimit += 20;", script);
        Assert.Contains("id=\"councilAgenda\"", html);
        Assert.Contains("id=\"turnForecastSummary\"", html);
        Assert.Contains("id=\"commandStream\"", html);
        Assert.Contains("id=\"strategicWatchSummary\"", html);
        Assert.Contains("Order Queue &amp; Turn Calendar", html);
        Assert.Contains("function renderCommandWorkspace", script);
        Assert.Contains("function commandAgendaItems", script);
        Assert.DoesNotContain("frontierSchematic", html);
        Assert.DoesNotContain("frontierSchematic", script);
        Assert.DoesNotContain("renderFrontierSchematic", script);
        Assert.DoesNotContain("commandFrontierSystems", script);
        Assert.DoesNotContain("schematic-", html);
        Assert.Contains("class=\"calendar-turn", script);
        Assert.Contains("data-cancel-order-id", script);
        Assert.Contains("function confirmOrderReplacement", script);
        Assert.Contains("window.confirm(", script);
        Assert.Contains("replacesOrderId", script);
        Assert.Contains("Superseded", html);
        Assert.Contains("Replaced by ${escapeHtml(formatOrderIntent(replacement))}", script);

        var overview = html.IndexOf("class=\"command-overview-grid\"", StringComparison.Ordinal);
        var agenda = html.IndexOf("id=\"councilAgenda\"", overview, StringComparison.Ordinal);
        var forecast = html.IndexOf("id=\"turnForecastSummary\"", agenda, StringComparison.Ordinal);
        var queue = html.IndexOf("id=\"orderQueueSection\"", forecast, StringComparison.Ordinal);
        var intelligence = html.IndexOf("class=\"command-rail\"", queue, StringComparison.Ordinal);
        Assert.True(overview >= 0);
        Assert.True(agenda > overview);
        Assert.True(forecast > agenda);
        Assert.True(queue > forecast);
        Assert.True(intelligence > queue);
    }

    [Fact]
    public void In_transit_fleets_remain_visible_as_ongoing_commitments()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("id=\"fleetRosterSummary\"", html);
        Assert.Contains("function transitCommitments()", script);
        Assert.Contains("item.fleet.status === \"inTransit\"", script);
        Assert.Contains("function operationalFleetItems()", script);
        Assert.Contains("function transitCalendarCard(transit)", script);
        Assert.Contains("Move underway", script);
        Assert.Contains("continues automatically", script);
        Assert.Contains("dispatched T${order.processedTick}", script);
        Assert.Contains("Departed from", script);
        Assert.Contains("recall available", script);
        Assert.Contains("active · ${formatNumber(transitFleetCount)} in transit", script);
        Assert.Contains("Command availability", script);
        Assert.Contains("Recall is the only available change", script);
        Assert.Contains("class=\"fleet-transit-track\"", script);
        Assert.DoesNotContain("No pending intention. This fleet is ready for a command.", script);
        Assert.Contains(".status-intransit", css);
        Assert.Contains(".fleet-item.is-in-transit", css);
        Assert.Contains(".fleet-transit-track", css);
    }

    [Fact]
    public void In_transit_fleets_can_queue_and_cancel_a_fleets_first_recall()
    {
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("data-recall-fleet-id", script);
        Assert.Contains("async function recallFleet(fleetId)", script);
        Assert.Contains("gameApi.postJson(\"/orders/recall\"", script);
        Assert.Contains("Recall ${transit.fleetName} to ${transit.originSystemName}?", script);
        Assert.Contains("The original move remains in history.", script);
        Assert.Contains("Recall ordered", script);
        Assert.Contains("projected return", script);
        Assert.Contains("Cancel recall", script);
        Assert.Contains("function projectedReturnCalendarCard(transit)", script);
        Assert.Contains("recallFleet: \"R\"", script);
        Assert.Contains("recalled T${order.processedTick} · returns T${transit.arrivalTickNumber}", script);
        Assert.Contains(".fleet-journey-actions", css);
    }

    [Fact]
    public void History_view_separates_filterable_chronicle_and_event_records()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("id=\"historyView\"", html);
        Assert.Contains("data-history-tab=\"chronicle\"", html);
        Assert.Contains("data-history-tab=\"events\"", html);
        Assert.Contains("id=\"chronicleSearch\"", html);
        Assert.Contains("id=\"chronicleImportance\"", html);
        Assert.Contains("id=\"chronicleSort\"", html);
        Assert.Contains("id=\"eventSearch\"", html);
        Assert.Contains("id=\"eventSeverity\"", html);
        Assert.Contains("id=\"eventSort\"", html);
        Assert.Contains("<option value=\"resolution\">Resolution order</option>", html);
        Assert.Contains("Results are grouped by authoritative phase.", html);
        Assert.Contains("Importance ${formatNumber(entry.importanceScore)}", script);
        Assert.Contains("T${entry.tickNumber ?? \"?\"}", script);
        Assert.Contains("class=\"chronicle-summary\"", script);
        Assert.Contains("function renderPhaseOrderedEvents(events)", script);
        Assert.Contains("eventResolutionPhaseOrder(left) - eventResolutionPhaseOrder(right)", script);
        Assert.Contains("event.resolutionPhaseOrder", script);
        Assert.Contains("event.resolutionPhase", script);
    }

    [Fact]
    public void Fleet_actions_use_the_roster_selection_as_their_command_context()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("data-fleet-tab=\"command\"", html);
        Assert.Contains("data-fleet-tab=\"history\"", html);
        Assert.Contains("data-fleet-action=\"move\"", html);
        Assert.Contains("data-fleet-action=\"attack\"", html);
        Assert.Contains("data-fleet-action=\"colonise\"", html);
        Assert.True(
            html.IndexOf("id=\"fleetSection\"", StringComparison.Ordinal) <
            html.IndexOf("id=\"ordersSection\"", StringComparison.Ordinal));
        Assert.DoesNotContain("id=\"fleetSelect\"", html);
        Assert.DoesNotContain("id=\"attackFleetSelect\"", html);
        Assert.DoesNotContain("id=\"coloniseFleetSelect\"", html);
        Assert.Contains("const fleetId = state.selectedFleetId;", script);
    }

    [Fact]
    public void Attack_action_requires_a_visible_local_hostile()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("targetFactions.length === 0", script);
        Assert.Contains("No hostile active fleet is present in this system.", script);
        Assert.Contains("!fleetReady || targetFactions.length === 0", script);
        Assert.Matches(
            new Regex(@"function collectTargetFactions\(selectedFleet\).*activeFleetsInSystem.*fleet\.factionId !== selectedFleet\.fleet\.factionId", RegexOptions.Singleline),
            script);
    }

    [Fact]
    public void Training_guide_points_to_the_required_fleet_before_its_action_form()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("activateFleetAction(\"move\")", script);
        Assert.Contains("return { element: trainingFleetTarget(\"Home Guard\") };", script);
        Assert.Contains("activateFleetAction(\"attack\")", script);
        Assert.Contains("return { element: trainingFleetTarget(\"Vanguard\") };", script);
        Assert.Contains("document.querySelector(`[data-fleet-id=\"${item.fleet.fleetId}\"]`)", script);
    }

    [Fact]
    public void Council_agenda_uses_the_typed_opening_briefing_without_event_json_parsing()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("openingBriefing, turnResolution } = bootstrap", script);
        Assert.Contains("const briefing = state.openingBriefing;", script);
        Assert.Contains("briefing?.objectives?.move", script);
        Assert.DoesNotContain("event.factJson", script);
        Assert.DoesNotContain("JSON.parse(event.factJson)", script);
    }

    [Fact]
    public void Training_guide_opens_with_the_players_starting_admiral_and_adaptive_panel()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("id=\"tutorialAdmiralPortrait\"", html);
        Assert.Contains("id=\"tutorialAdmiralName\"", html);
        Assert.Contains("role=\"complementary\"", html);
        Assert.Contains("id=\"tutorialRequirement\" class=\"tutorial-requirement\" role=\"status\"", html);
        Assert.DoesNotContain("id=\"tutorialTitle\" tabindex", html);
        Assert.DoesNotContain("id=\"tutorialAdmiralPortrait\" src=", html);
        Assert.Contains("astrolabe-gold-human-01", script);
        Assert.Contains("item.fleet.empireId === state.empire?.empireId && item.admiral", script);
        Assert.Contains("stableTutorialPortraitIndex(admiral?.admiralId", script);
        Assert.Contains("renderTutorialAdmiral();", script);
        Assert.Contains("!narrow && tutorialPanelShouldSitOnRight(target.element)", script);
        Assert.Contains("target.getBoundingClientRect()", script);
        Assert.DoesNotContain("tutorialTitle.focus", script);
        Assert.DoesNotContain("renderTutorial({ focusHeading", script);
        Assert.Contains(".tutorial-panel.is-right", css);
        Assert.Contains(".tutorial-admiral-frame", css);
    }

    [Fact]
    public void Training_guide_can_start_fresh_without_adding_another_toolbar_action()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("id=\"tutorialResetButton\"", html);
        Assert.Contains(">Start fresh</button>", html);
        Assert.Contains("tutorialResetButton.addEventListener(\"click\", resetTutorial)", script);
        Assert.Contains("function resetTutorial()", script);
        Assert.DoesNotContain("<button id=\"tutorialResetButton\"", html[..html.IndexOf("<main", StringComparison.Ordinal)]);
    }

    [Fact]
    public void Training_guide_offers_explicit_targeting_and_keeps_narrow_actions_visible()
    {
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("function trainingTutorialTarget(lesson)", script);
        Assert.Contains("elements.tutorialShowMeButton.addEventListener(\"click\", showTrainingTutorialTarget)", script);
        Assert.Contains("const target = trainingTutorialTarget(lesson);", script);
        Assert.Contains("applyTutorialTarget(target.element, { describe });", script);
        Assert.Matches(
            new Regex(@"\.tutorial-actions\s*\{[^}]*position:\s*sticky;[^}]*bottom:\s*0;", RegexOptions.Singleline),
            css);
        Assert.Matches(
            new Regex(@"@media \(max-width: 560px\)[\s\S]*?\.tutorial-actions\s*\{[^}]*grid-template-columns:\s*repeat\(2,\s*minmax\(0,\s*1fr\)\);", RegexOptions.Singleline),
            css);
    }

    [Fact]
    public void Dashboard_displays_safe_error_messages_and_retains_machine_codes()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("new Error(payload.message ?? \"Request failed.\")", script);
        Assert.Contains("error.code = payload.code ?? \"requestFailed\";", script);
        Assert.Contains("error.details = payload.details ?? null;", script);
        Assert.Contains("error.traceId = payload.traceId ?? null;", script);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));
}
