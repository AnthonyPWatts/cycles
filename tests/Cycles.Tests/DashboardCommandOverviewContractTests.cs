namespace Cycles.Tests;

public sealed class DashboardCommandOverviewContractTests
{
    [Fact]
    public void Command_overview_keeps_resources_and_linked_priorities_visible_together()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("id=\"strategicWatchSummary\" class=\"strategic-watch-list\"", html);
        Assert.Contains("id=\"resourcesSection\" class=\"command-resources\"", html);
        Assert.Contains("id=\"homeSystemName\"", html);
        Assert.Contains("id=\"prioritySection\" class=\"priority-console\"", html);
        Assert.Contains("data-priority-key=\"industryWeight\" type=\"range\" min=\"0\" max=\"100\" step=\"1\" value=\"0\" aria-describedby=\"priorityModelNote\" disabled", html);
        Assert.Contains("data-priority-key=\"researchWeight\" type=\"range\" min=\"0\" max=\"100\" step=\"1\" value=\"0\" aria-describedby=\"priorityModelNote\" disabled", html);
        Assert.Contains("data-priority-key=\"militaryWeight\" type=\"range\"", html);
        Assert.Contains("data-priority-key=\"expansionWeight\" type=\"range\"", html);
        Assert.Contains("id=\"priorityModelNote\" class=\"priority-model-note\"", html);
        Assert.Contains("Development <small class=\"priority-effect-status priority-effect-status--inactive\">Locked</small>", html);
        Assert.Contains("Innovation <small class=\"priority-effect-status priority-effect-status--inactive\">Locked</small>", html);
        Assert.Contains("Military <small class=\"priority-effect-status priority-effect-status--active\">Active</small>", html);
        Assert.Contains("Expansion <small class=\"priority-effect-status priority-effect-status--active\">Active</small>", html);
        Assert.Contains("id=\"priorityResetButton\"", html);
        Assert.Contains("id=\"prioritySaveButton\"", html);
        Assert.Contains("id=\"priorityDraftStatus\"", html);
        Assert.Contains("class=\"priority-channel-name\"", html);
        Assert.Contains("class=\"priority-saved-marker\"", html);
        Assert.DoesNotContain("id=\"priorityHint\"", html);
        Assert.Contains("id=\"advanceTurnDialog\" class=\"advance-turn-dialog\"", html);
        Assert.Contains("elements.advanceTurnDialog.returnValue = \"\";", script);
        Assert.DoesNotContain("id=\"adjustPrioritiesButton\"", html);

        Assert.Contains("elements.homeSystemName.textContent = empire.homeSystem.systemName;", script);
        Assert.DoesNotContain("resource-home", script);
        Assert.Contains("rebalancePriorityDraft(input.dataset.priorityKey", script);
        Assert.Contains("const activePriorityKeys = [\"militaryWeight\", \"expansionWeight\"]", script);
        Assert.Contains("input.disabled = state.prioritySaving || isInactive || !commandsAreOpen();", script);
        Assert.Contains("sliderShell.classList.toggle(\"has-saved-marker\", isChanged);", script);
        Assert.Contains("elements.priorityDraftStatus.textContent = state.prioritySaving ? \"Saving\" : isDirty ? \"Unsaved\" : \"Saved\";", script);
        Assert.Contains("elements.prioritySaveButton.disabled = !isDirty || total !== 100 || state.prioritySaving || !commandsAreOpen();", script);
        Assert.Contains("elements.priorityResetButton.disabled = !isDirty || state.prioritySaving;", script);
        Assert.Contains("if (lesson.key === \"T1\")", script);
        Assert.Contains("element: elements.prioritySection", script);
        Assert.Contains("focusElement: document.querySelector(\"#militaryWeight\")", script);
    }

    [Fact]
    public void Command_explains_the_complete_turn_contract_and_distinguishes_forecasts_from_commitments()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("id=\"turnResolutionSection\"", html);
        Assert.Contains("id=\"turnStageBadge\"", html);
        Assert.Contains("id=\"turnPhaseOrder\"", html);
        Assert.Contains("aria-label=\"Authoritative turn processing order\"", html);
        Assert.Contains("Submission time grants no initiative", html);
        Assert.Contains("Projections can change before closure; committed deliveries are authoritative.", html);
        Assert.Contains("id=\"turnForecastSummary\"", html);

        Assert.Contains("turnResolution.phases.map", script);
        Assert.Contains("Stable ordering only makes the sealed result reproducible.", script);
        Assert.Contains("Calculated from current influence before movement.", script);
        Assert.Contains("Projected reservation · closure", script);
        Assert.Contains("Projected programme · phase 3", script);
        Assert.Contains("Authoritative commitments", script);
        Assert.Contains("Projected progression · phase 8", script);
        Assert.Contains("No player orders queued", script);
        Assert.Contains("Automatic income, programmes, or deliveries still resolve", script);
        Assert.Contains("No player orders or scheduled effects", script);
        Assert.Contains("function turnForecastCalendarCards(tick)", script);
        Assert.Contains("Committed ship delivery", script);
        Assert.Contains("Projected ship delivery", script);

        Assert.Contains(".turn-phase-order", css);
        Assert.Contains("grid-template-columns: repeat(3, minmax(0, 1fr));", css);
        Assert.Contains(".turn-forecast-grid", css);
        Assert.Contains(".turn-forecast-item.is-commitment", css);
        Assert.Contains("@media (max-width: 560px)", css);
    }

    [Fact]
    public void Development_turn_action_warns_that_it_closes_the_current_game_globally()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("Close command window and advance", html);
        Assert.Contains("Close this game's command window?", html);
        Assert.Contains("one game-wide ledger will seal", html);
        Assert.Contains("advances the whole current game, not only your empire", html);
        Assert.Contains("turn.gamePendingHumanOrderCount", script);
        Assert.Contains("turn.gameFleetIntentionCount", script);
        Assert.Contains("elements.advanceTurnDialog.showModal();", script);
        Assert.Contains("Published T${result.tickNumber}", script);
        Assert.Contains("Display order did not grant initiative.", script);
    }

    [Fact]
    public void Training_resolution_uses_the_authoritative_journey_evidence()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("lesson.mechanicalEvidence.summary", script);
        Assert.Contains("lesson.mechanicalEvidence.satisfied", script);
        Assert.Contains("lesson.presentationAcknowledgement.required", script);
        Assert.Contains("/tutorial/acknowledgements", script);
        Assert.Contains("/tutorial/resolve", script);
        Assert.Contains("result.journey", script);
    }

    [Fact]
    public void Colonisation_command_and_results_explain_whole_set_reservation()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("100 population reserved at closure", script);
        Assert.Contains("function colonisationReservationHint()", script);
        Assert.Contains("Current-turn Population income counts when commands close", script);
        Assert.Contains("the whole set is rejected if the final budget is short", script);
        Assert.Contains("const rejection = order.rejectionReason", script);
        Assert.Contains("${escapeHtml(timing)}${escapeHtml(rejection)}", script);
    }

    [Fact]
    public void Opening_agenda_move_uses_the_objective_target_and_rejects_an_unavailable_target()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("data-command-target-system", script);
        Assert.Contains("selectCommandMoveTarget(fleetButton.dataset.commandTargetSystem);", script);
        Assert.Contains("function selectCommandMoveTarget(targetSystemId)", script);
        Assert.Contains("option => option.value === targetSystemId", script);
        Assert.Contains("elements.destinationSelect.value = targetIsAvailable ? targetSystemId : \"\";", script);
        Assert.Contains("The briefing destination is no longer available from this fleet's current system.", script);
    }

    [Fact]
    public void Move_command_repeats_projected_journey_timing_before_and_after_submission()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("id=\"destinationSelect\" aria-describedby=\"moveActionHint\"", html);
        Assert.Contains("id=\"moveActionHint\" class=\"form-hint\" role=\"status\"", html);
        Assert.Contains("state.fleetDetail.legalMoveDestinations", script);
        Assert.Contains("function moveDestinationOptionLabel(destination)", script);
        Assert.Contains("function renderMoveActionHint()", script);
        Assert.Contains("function formatJourneyDuration(travelTicks)", script);
        Assert.Contains("projected dispatch T${formatNumber(destination.projectedDispatchTickNumber)}", script);
        Assert.Contains("projected arrival T${formatNumber(destination.projectedArrivalTickNumber)}", script);
        Assert.Contains("The route and timing are revalidated when the command activates.", script);
        Assert.Contains("elements.destinationSelect.addEventListener(\"change\", renderMoveActionHint);", script);
        Assert.Contains("order.moveJourneyProjection", script);
        Assert.Contains("current route unavailable; dispatch and arrival will be revalidated", script);
        Assert.Contains("formatJourneyDuration(projection.travelTicks)", script);
        Assert.Contains("Dispatched T${formatTickNumber(transit?.dispatchedTickNumber)}", script);
        Assert.Contains("Projected reversal T${formatTickNumber(transit.recallExecuteTickNumber)}", script);
        Assert.Contains("item.fleet.arrivalTickNumber !== null", script);
    }

    [Fact]
    public void Active_priority_sliders_transfer_points_between_each_other()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("const pointDelta = activeValue - state.priorityDraft[activeKey];", script);
        Assert.Contains("if (!activePriorityKeys.includes(activeKey))", script);
        Assert.Contains("const otherKeys = activePriorityKeys.filter(key => key !== activeKey);", script);
        Assert.Contains("for (let point = 0; point < Math.abs(pointDelta); point += 1)", script);
        Assert.Contains("? candidateValue > selectedValue", script);
        Assert.Contains(": candidateValue < selectedValue;", script);
        Assert.Contains("state.priorityDraft[transferKey] -= Math.sign(pointDelta);", script);
        Assert.DoesNotContain("remaining * weight / weightTotal", script);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));
}
