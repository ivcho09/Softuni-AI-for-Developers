import { ApiError, projects, statistics, tasks } from "./api.js";

const PRIORITIES = ["Low", "Medium", "High", "Urgent"];
const STATUSES = ["Pending", "InProgress", "Completed"];
const STATUS_LABELS = { Pending: "Pending", InProgress: "In progress", Completed: "Completed" };
const PAGE_SIZE = 20;

const state = {
    projects: [],
    selectedProjectId: null,  // null means "all tasks"
    page: 1,
    filters: { search: "", status: "", priority: "", overdue: "", sortBy: "CreatedAt", descending: false },
    editingTaskId: null,
    editingProjectId: null
};

const $ = (id) => document.getElementById(id);

// --- Dates ----------------------------------------------------------------
// SQLite round-trips DateTime values without a kind, so timestamps can arrive
// as "2026-11-03T10:00:00" with no zone designator. They are always UTC, and
// JavaScript would otherwise read a bare timestamp as local time.
function parseUtc(value) {
    if (!value) {
        return null;
    }

    const hasZone = /(?:Z|[+-]\d{2}:?\d{2})$/.test(value);
    return new Date(hasZone ? value : `${value}Z`);
}

function formatDate(value) {
    const date = parseUtc(value);
    if (!date) {
        return "";
    }

    return date.toLocaleString(undefined, {
        year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit"
    });
}

/** Converts a UTC timestamp into the local value a datetime-local input expects. */
function toLocalInputValue(value) {
    const date = parseUtc(value);
    if (!date) {
        return "";
    }

    const pad = (n) => String(n).padStart(2, "0");
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
        + `T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** Converts a datetime-local value, which the browser reports in local time, into UTC. */
function toUtcIso(localValue) {
    if (!localValue) {
        return null;
    }

    const date = new Date(localValue);
    return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

// --- Toasts ---------------------------------------------------------------
function toast(message, kind = "info") {
    const element = document.createElement("div");
    element.className = `toast ${kind}`;
    element.textContent = message;
    $("toasts").append(element);
    setTimeout(() => element.remove(), 4000);
}

/**
 * Runs an API call, reporting failures as a toast. Validation errors are left to
 * the caller when it passes a form, so they can be shown next to the fields.
 */
async function run(action, { onValidationError } = {}) {
    try {
        return await action();
    } catch (error) {
        if (!(error instanceof ApiError)) {
            throw error;
        }

        if (error.errors && onValidationError) {
            onValidationError(error);
        } else {
            toast(error.message, "error");
        }

        return undefined;
    }
}

// --- Rendering: statistics ------------------------------------------------
function renderStatistics(data) {
    const cards = [
        { label: "Total", value: data.totalTasks },
        { label: "Completed", value: data.completedTasks, className: "is-done" },
        { label: "In progress", value: data.inProgressTasks },
        { label: "Pending", value: data.pendingTasks },
        { label: "Overdue", value: data.overdueTasks, className: data.overdueTasks > 0 ? "is-overdue" : "" },
        { label: "Done", value: `${data.completionPercentage}%` }
    ];

    $("stat-cards").innerHTML = cards
        .map((card) => `
            <div class="stat-card ${card.className ?? ""}">
                <div class="value">${card.value}</div>
                <div class="label">${card.label}</div>
            </div>`)
        .join("");

    const breakdown = (title, counts, order) => {
        const max = Math.max(1, ...Object.values(counts));
        const rows = order
            .map((key) => {
                const count = counts[key] ?? 0;
                const label = STATUS_LABELS[key] ?? key;
                return `
                    <div class="bar-row">
                        <span>${label}</span>
                        <span class="bar"><span style="width: ${(count / max) * 100}%"></span></span>
                        <span class="count">${count}</span>
                    </div>`;
            })
            .join("");

        return `<div class="breakdown"><h3>${title}</h3>${rows}</div>`;
    };

    $("stat-breakdown").innerHTML =
        breakdown("By status", data.tasksByStatus ?? {}, STATUSES)
        + breakdown("By priority", data.tasksByPriority ?? {}, [...PRIORITIES].reverse());
}

async function loadStatistics() {
    const data = await run(() => (state.selectedProjectId === null
        ? statistics.overall()
        : statistics.forProject(state.selectedProjectId)));

    if (data) {
        renderStatistics(data);
    }
}

// --- Rendering: projects --------------------------------------------------
function renderProjects() {
    const totalTasks = state.projects.reduce((sum, project) => sum + project.taskCount, 0);

    const allItem = `
        <li class="project-item ${state.selectedProjectId === null ? "active" : ""}" data-project-id="">
            <span class="name">All tasks</span>
            <span class="badge">${totalTasks}</span>
        </li>`;

    const items = state.projects
        .map((project) => `
            <li class="project-item ${state.selectedProjectId === project.id ? "active" : ""}"
                data-project-id="${project.id}" title="${escapeAttribute(project.description ?? project.name)}">
                <span class="name">${escapeHtml(project.name)}</span>
                <span class="badge">${project.taskCount}</span>
                <span class="row-actions">
                    <button type="button" class="icon-btn" data-edit-project="${project.id}" title="Edit project">&#9998;</button>
                    <button type="button" class="icon-btn" data-delete-project="${project.id}" title="Delete project">&#10005;</button>
                </span>
            </li>`)
        .join("");

    $("project-list").innerHTML = allItem + items;
}

async function loadProjects() {
    const data = await run(() => projects.list());
    if (!data) {
        return;
    }

    state.projects = data;

    // A project may have been deleted in another tab; fall back to all tasks.
    if (state.selectedProjectId !== null
        && !data.some((project) => project.id === state.selectedProjectId)) {
        state.selectedProjectId = null;
    }

    renderProjects();
    renderProjectOptions();
}

function renderProjectOptions() {
    const select = $("task-project");
    const current = select.value;

    select.innerHTML = `<option value="">No project</option>`
        + state.projects
            .map((project) => `<option value="${project.id}">${escapeHtml(project.name)}</option>`)
            .join("");

    select.value = current;
}

// --- Rendering: tasks -----------------------------------------------------
function renderTasks(paged) {
    const list = $("task-list");

    if (paged.items.length === 0) {
        list.innerHTML = `<li class="empty">No tasks match the current filters.</li>`;
    } else {
        list.innerHTML = paged.items.map(taskMarkup).join("");
    }

    const selected = state.projects.find((project) => project.id === state.selectedProjectId);
    $("list-title").textContent = selected ? selected.name : "All tasks";
    $("list-count").textContent = paged.totalCount === 1 ? "1 task" : `${paged.totalCount} tasks`;

    renderPager(paged);
}

function taskMarkup(task) {
    const completed = task.status === "Completed";

    const meta = [
        `<span class="chip status-${task.status}">${STATUS_LABELS[task.status] ?? task.status}</span>`,
        `<span class="chip priority-${task.priority}">${task.priority}</span>`
    ];

    if (task.projectName) {
        meta.push(`<span class="chip">${escapeHtml(task.projectName)}</span>`);
    }

    if (task.dueDate) {
        const label = `Due ${formatDate(task.dueDate)}`;
        meta.push(`<span class="chip ${task.isOverdue ? "overdue" : ""}">${task.isOverdue ? "Overdue - " : ""}${label}</span>`);
    }

    if (completed && task.completedAt) {
        meta.push(`<span class="chip">Completed ${formatDate(task.completedAt)}</span>`);
    }

    return `
        <li class="task ${completed ? "completed" : ""}" data-priority="${task.priority}" data-task-id="${task.id}">
            <input type="checkbox" class="task-check" ${completed ? "checked" : ""}
                   data-toggle-task="${task.id}" title="${completed ? "Reopen task" : "Mark as completed"}"
                   aria-label="${completed ? "Reopen task" : "Mark as completed"}" />
            <div>
                <div class="task-title">${escapeHtml(task.title)}</div>
                ${task.description ? `<p class="task-desc">${escapeHtml(task.description)}</p>` : ""}
                <div class="task-meta">${meta.join("")}</div>
            </div>
            <div class="task-actions">
                <button type="button" class="btn btn-small" data-edit-task="${task.id}">Edit</button>
                <button type="button" class="btn btn-small btn-danger" data-delete-task="${task.id}">Delete</button>
            </div>
        </li>`;
}

function renderPager(paged) {
    if (paged.totalPages <= 1) {
        $("pager").innerHTML = "";
        return;
    }

    $("pager").innerHTML = `
        <button type="button" class="btn btn-small" data-page="${paged.page - 1}"
                ${paged.hasPreviousPage ? "" : "disabled"}>Previous</button>
        <span class="page-info">Page ${paged.page} of ${paged.totalPages}</span>
        <button type="button" class="btn btn-small" data-page="${paged.page + 1}"
                ${paged.hasNextPage ? "" : "disabled"}>Next</button>`;
}

async function loadTasks() {
    const { search, status, priority, overdue, sortBy, descending } = state.filters;

    const paged = await run(() => tasks.list({
        search,
        status,
        priority,
        overdue,
        sortBy,
        descending,
        projectId: state.selectedProjectId ?? "",
        page: state.page,
        pageSize: PAGE_SIZE
    }));

    if (paged) {
        renderTasks(paged);
    }
}

/** Reloads everything that a write can affect. */
async function refresh() {
    await Promise.all([loadProjects(), loadTasks(), loadStatistics()]);
}

// --- Escaping -------------------------------------------------------------
function escapeHtml(value) {
    return String(value).replace(/[&<>"']/g, (char) => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
    })[char]);
}

const escapeAttribute = escapeHtml;

// --- Forms ----------------------------------------------------------------
function clearErrors(form) {
    form.querySelectorAll(".field-error").forEach((element) => { element.textContent = ""; });
    form.querySelector(".form-error").textContent = "";
}

/**
 * Places each validation message from the API under its field. The server keys
 * errors by property name, which may arrive as "Title" or "$.title".
 */
function showErrors(form, error) {
    clearErrors(form);

    const unmatched = [];

    for (const [key, messages] of Object.entries(error.errors ?? {})) {
        const field = key.replace(/^\$\./, "").split(".").pop();
        const name = field.charAt(0).toLowerCase() + field.slice(1);
        const target = form.querySelector(`[data-error-for="${name}"]`);

        if (target) {
            target.textContent = messages.join(" ");
        } else {
            unmatched.push(messages.join(" "));
        }
    }

    form.querySelector(".form-error").textContent = unmatched.join(" ") || (
        Object.keys(error.errors ?? {}).length === 0 ? error.message : ""
    );
}

function openTaskDialog(task) {
    const form = $("task-form");
    clearErrors(form);

    state.editingTaskId = task?.id ?? null;
    $("task-dialog-title").textContent = task ? "Edit task" : "New task";
    $("task-submit").textContent = task ? "Save changes" : "Create task";

    $("task-title").value = task?.title ?? "";
    $("task-description").value = task?.description ?? "";
    $("task-status").value = task?.status ?? "Pending";
    $("task-priority").value = task?.priority ?? "Medium";
    $("task-due").value = toLocalInputValue(task?.dueDate);

    renderProjectOptions();
    $("task-project").value = String(task?.projectId ?? state.selectedProjectId ?? "");

    $("task-dialog").showModal();
    $("task-title").focus();
}

function openProjectDialog(project) {
    const form = $("project-form");
    clearErrors(form);

    state.editingProjectId = project?.id ?? null;
    $("project-dialog-title").textContent = project ? "Edit project" : "New project";
    $("project-submit").textContent = project ? "Save changes" : "Create project";
    $("project-name").value = project?.name ?? "";
    $("project-description").value = project?.description ?? "";

    $("project-dialog").showModal();
    $("project-name").focus();
}

// --- Event wiring ---------------------------------------------------------
function wireDialogs() {
    document.querySelectorAll(".modal [data-close]").forEach((button) => {
        button.addEventListener("click", () => button.closest(".modal").close());
    });

    $("task-form").addEventListener("submit", async (event) => {
        event.preventDefault();
        const form = event.currentTarget;

        const projectId = $("task-project").value;
        const payload = {
            title: $("task-title").value.trim(),
            description: $("task-description").value.trim() || null,
            status: $("task-status").value,
            priority: $("task-priority").value,
            dueDate: toUtcIso($("task-due").value),
            projectId: projectId === "" ? null : Number(projectId)
        };

        if (payload.title === "") {
            form.querySelector(`[data-error-for="title"]`).textContent = "Title is required.";
            return;
        }

        const id = state.editingTaskId;
        const saved = await run(
            () => (id === null ? tasks.create(payload) : tasks.update(id, payload)),
            { onValidationError: (error) => showErrors(form, error) }
        );

        if (saved) {
            $("task-dialog").close();
            toast(id === null ? "Task created." : "Task updated.", "success");
            await refresh();
        }
    });

    $("project-form").addEventListener("submit", async (event) => {
        event.preventDefault();
        const form = event.currentTarget;

        const payload = {
            name: $("project-name").value.trim(),
            description: $("project-description").value.trim() || null
        };

        if (payload.name === "") {
            form.querySelector(`[data-error-for="name"]`).textContent = "Name is required.";
            return;
        }

        const id = state.editingProjectId;
        const saved = await run(
            () => (id === null ? projects.create(payload) : projects.update(id, payload)),
            { onValidationError: (error) => showErrors(form, error) }
        );

        if (saved) {
            $("project-dialog").close();
            toast(id === null ? "Project created." : "Project updated.", "success");
            await refresh();
        }
    });
}

function wireFilters() {
    let searchTimer;

    $("filter-search").addEventListener("input", (event) => {
        clearTimeout(searchTimer);
        const value = event.target.value.trim();
        searchTimer = setTimeout(() => {
            state.filters.search = value;
            state.page = 1;
            loadTasks();
        }, 250);
    });

    const bind = (id, key) => $(id).addEventListener("change", (event) => {
        state.filters[key] = event.target.value;
        state.page = 1;
        loadTasks();
    });

    bind("filter-status", "status");
    bind("filter-priority", "priority");
    bind("filter-overdue", "overdue");

    $("filter-sort").addEventListener("change", (event) => {
        state.filters.sortBy = event.target.value;
        // Most urgent first is the useful default when sorting by priority.
        state.filters.descending = event.target.value === "Priority";
        updateDirectionButton();
        state.page = 1;
        loadTasks();
    });

    $("filter-direction").addEventListener("click", () => {
        state.filters.descending = !state.filters.descending;
        updateDirectionButton();
        state.page = 1;
        loadTasks();
    });

    $("filter-reset").addEventListener("click", () => {
        state.filters = {
            search: "", status: "", priority: "", overdue: "",
            sortBy: "CreatedAt", descending: false
        };
        $("filter-search").value = "";
        $("filter-status").value = "";
        $("filter-priority").value = "";
        $("filter-overdue").value = "";
        $("filter-sort").value = "CreatedAt";
        updateDirectionButton();
        state.page = 1;
        loadTasks();
    });
}

function updateDirectionButton() {
    const button = $("filter-direction");
    button.dataset.descending = String(state.filters.descending);
    button.textContent = state.filters.descending ? "Descending" : "Ascending";
}

function wireLists() {
    $("project-list").addEventListener("click", async (event) => {
        const editId = event.target.closest("[data-edit-project]")?.dataset.editProject;
        if (editId) {
            const project = state.projects.find((candidate) => candidate.id === Number(editId));
            openProjectDialog(project);
            return;
        }

        const deleteId = event.target.closest("[data-delete-project]")?.dataset.deleteProject;
        if (deleteId) {
            const project = state.projects.find((candidate) => candidate.id === Number(deleteId));
            const confirmed = confirm(
                `Delete the project "${project?.name}"?\n\n`
                + "Its tasks are kept and simply detached from the project."
            );

            if (confirmed) {
                const result = await run(() => projects.remove(Number(deleteId)));
                if (result === null) {
                    if (state.selectedProjectId === Number(deleteId)) {
                        state.selectedProjectId = null;
                    }
                    toast("Project deleted. Its tasks were kept.", "success");
                    await refresh();
                }
            }
            return;
        }

        const row = event.target.closest(".project-item");
        if (row) {
            const value = row.dataset.projectId;
            state.selectedProjectId = value === "" ? null : Number(value);
            state.page = 1;
            renderProjects();
            await Promise.all([loadTasks(), loadStatistics()]);
        }
    });

    $("task-list").addEventListener("click", async (event) => {
        const toggleId = event.target.closest("[data-toggle-task]")?.dataset.toggleTask;
        if (toggleId) {
            const checked = event.target.checked;
            const id = Number(toggleId);
            const updated = await run(() => (checked ? tasks.complete(id) : tasks.reopen(id)));

            if (updated) {
                await Promise.all([loadTasks(), loadStatistics()]);
            } else {
                event.target.checked = !checked;  // the call failed, so undo the tick
            }
            return;
        }

        const editId = event.target.closest("[data-edit-task]")?.dataset.editTask;
        if (editId) {
            const task = await run(() => tasks.get(Number(editId)));
            if (task) {
                openTaskDialog(task);
            }
            return;
        }

        const deleteId = event.target.closest("[data-delete-task]")?.dataset.deleteTask;
        if (deleteId && confirm("Delete this task permanently?")) {
            const result = await run(() => tasks.remove(Number(deleteId)));
            if (result === null) {
                toast("Task deleted.", "success");
                await refresh();
            }
        }
    });

    $("pager").addEventListener("click", (event) => {
        const page = event.target.closest("[data-page]")?.dataset.page;
        if (page) {
            state.page = Number(page);
            loadTasks();
            window.scrollTo({ top: 0, behavior: "smooth" });
        }
    });
}

function wireTheme() {
    const stored = localStorage.getItem("taskflow-theme");
    if (stored) {
        document.documentElement.dataset.theme = stored;
    }

    const button = $("theme-toggle");
    const sync = () => {
        button.textContent = document.documentElement.dataset.theme === "light"
            ? "Dark mode"
            : "Light mode";
    };

    button.addEventListener("click", () => {
        const next = document.documentElement.dataset.theme === "light" ? "dark" : "light";
        document.documentElement.dataset.theme = next;
        localStorage.setItem("taskflow-theme", next);
        sync();
    });

    sync();
}

function init() {
    wireTheme();
    wireDialogs();
    wireFilters();
    wireLists();
    updateDirectionButton();

    $("new-task-btn").addEventListener("click", () => openTaskDialog(null));
    $("new-project-btn").addEventListener("click", () => openProjectDialog(null));

    refresh();
}

init();
