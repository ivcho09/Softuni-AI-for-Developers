// Thin wrapper over the TaskFlow REST API. The frontend is served by the same
// application, so requests are relative and need no CORS configuration.

const BASE = "/api";

/**
 * A failed request. Carries the server's ErrorResponse so callers can show
 * field level validation messages next to the inputs that caused them.
 */
export class ApiError extends Error {
    constructor(status, message, errors) {
        super(message);
        this.name = "ApiError";
        this.status = status;
        this.errors = errors ?? null;
    }
}

async function request(path, { method = "GET", body } = {}) {
    let response;

    try {
        response = await fetch(BASE + path, {
            method,
            headers: body === undefined ? undefined : { "Content-Type": "application/json" },
            body: body === undefined ? undefined : JSON.stringify(body)
        });
    } catch {
        throw new ApiError(0, "Cannot reach the API. Is the server still running?");
    }

    if (response.status === 204) {
        return null;
    }

    const payload = await response.json().catch(() => null);

    if (!response.ok) {
        // Every failing endpoint returns the same ErrorResponse shape.
        throw new ApiError(
            response.status,
            payload?.message ?? `Request failed with status ${response.status}.`,
            payload?.errors
        );
    }

    return payload;
}

function queryString(params) {
    const search = new URLSearchParams();

    for (const [key, value] of Object.entries(params)) {
        if (value !== null && value !== undefined && value !== "") {
            search.set(key, value);
        }
    }

    const query = search.toString();
    return query ? `?${query}` : "";
}

export const tasks = {
    list: (params) => request(`/tasks${queryString(params)}`),
    get: (id) => request(`/tasks/${id}`),
    create: (task) => request("/tasks", { method: "POST", body: task }),
    update: (id, task) => request(`/tasks/${id}`, { method: "PUT", body: task }),
    remove: (id) => request(`/tasks/${id}`, { method: "DELETE" }),
    complete: (id) => request(`/tasks/${id}/complete`, { method: "POST" }),
    reopen: (id) => request(`/tasks/${id}/reopen`, { method: "POST" })
};

export const projects = {
    list: () => request("/projects"),
    get: (id) => request(`/projects/${id}`),
    tasks: (id) => request(`/projects/${id}/tasks`),
    create: (project) => request("/projects", { method: "POST", body: project }),
    update: (id, project) => request(`/projects/${id}`, { method: "PUT", body: project }),
    remove: (id) => request(`/projects/${id}`, { method: "DELETE" })
};

export const statistics = {
    overall: () => request("/statistics"),
    forProject: (projectId) => request(`/statistics/projects/${projectId}`)
};
