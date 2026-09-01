import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, expect, test } from "vitest";

import { App } from "./App";
import { mockApi } from "./test/setup";

beforeEach(() => {
  let notes: Array<Record<string, unknown>> = [
    {
      id: "11111111-1111-1111-1111-111111111111",
      title: "Seed note",
      body: "from the mock",
      createdOnUtc: new Date().toISOString(),
      updatedOnUtc: null,
    },
  ];

  mockApi(({ url, method, body }) => {
    if (url.endsWith("/api/notes") && method === "GET") {
      return notes;
    }
    if (url.endsWith("/api/notes") && method === "POST") {
      const created = {
        id: "22222222-2222-2222-2222-222222222222",
        title: body.title,
        body: body.body,
        createdOnUtc: new Date().toISOString(),
        updatedOnUtc: null,
      };
      notes = [created, ...notes];
      return new Response(JSON.stringify(created), {
        status: 201,
        headers: { "Content-Type": "application/json" },
      });
    }
    return new Response("not found", { status: 404 });
  });
});

test("renders the notes screen", () => {
  render(<App />);
  expect(screen.getByRole("heading", { name: /chassis — notes/i })).toBeInTheDocument();
});

test("lists notes returned by the API", async () => {
  render(<App />);
  expect(await screen.findByText("Seed note")).toBeInTheDocument();
});

test("creates a note through the typed client", async () => {
  const user = userEvent.setup();
  render(<App />);

  await screen.findByText("Seed note");
  await user.type(screen.getByLabelText("Title"), "Written in test");
  await user.type(screen.getByLabelText("Body"), "body text");
  await user.click(screen.getByRole("button", { name: /add note/i }));

  expect(await screen.findByText(/Written in test/)).toBeInTheDocument();
});
