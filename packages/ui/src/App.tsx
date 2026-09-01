import { useCallback, useEffect, useState } from "react";
import type { FormEvent } from "react";

import type { components } from "@chassis/api-client";

import { api } from "./api";

type Note = components["schemas"]["NoteDto"];

/**
 * Placeholder screen for the template (§11 step 2). It exercises the full path:
 * React → generated typed client (`@chassis/api-client`) → Chassis.Api → domain.
 * Replace with real screens in step 6.
 */
export function App() {
  const [notes, setNotes] = useState<Note[]>([]);
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [status, setStatus] = useState<"idle" | "loading" | "saving">("loading");
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    const { data, error } = await api.GET("/api/notes");
    if (error) {
      setError("Could not load notes. Is the API running?");
      return;
    }
    setNotes(data ?? []);
    setError(null);
  }, []);

  useEffect(() => {
    void refresh().finally(() => setStatus("idle"));
  }, [refresh]);

  async function addNote(event: FormEvent) {
    event.preventDefault();
    setStatus("saving");
    const { error } = await api.POST("/api/notes", { body: { title, body } });
    setStatus("idle");
    if (error) {
      setError("Could not create the note.");
      return;
    }
    setTitle("");
    setBody("");
    await refresh();
  }

  return (
    <main style={{ maxWidth: 640, margin: "3rem auto", fontFamily: "system-ui, sans-serif" }}>
      <h1>Chassis — Notes</h1>
      <p style={{ color: "#555" }}>
        Placeholder screen. Every call goes through the generated{" "}
        <code>@chassis/api-client</code>.
      </p>

      <form onSubmit={addNote} style={{ display: "flex", gap: "0.5rem", margin: "1.5rem 0" }}>
        <input
          aria-label="Title"
          placeholder="Title"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
        />
        <input
          aria-label="Body"
          placeholder="Body"
          value={body}
          onChange={(e) => setBody(e.target.value)}
        />
        <button type="submit" disabled={status === "saving" || title.trim() === ""}>
          {status === "saving" ? "Adding…" : "Add note"}
        </button>
      </form>

      {error ? <p role="alert" style={{ color: "#b00020" }}>{error}</p> : null}

      {status === "loading" ? (
        <p>Loading…</p>
      ) : notes.length === 0 ? (
        <p style={{ color: "#777" }}>No notes yet.</p>
      ) : (
        <ul>
          {notes.map((note) => (
            <li key={note.id}>
              <strong>{note.title}</strong>
              {note.body ? ` — ${note.body}` : ""}
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
