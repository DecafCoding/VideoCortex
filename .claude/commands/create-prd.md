---
description: Create a Product Requirements Document from a source document, folder, or conversation
argument-hint: <source-path> [output-filename]
---

# Create PRD: Generate Product Requirements Document

## Overview

Generate a comprehensive Product Requirements Document (PRD) from a source file or folder. Use the structure and sections defined below to create a thorough, professional PRD. **The PRD is written as a standalone HTML document** (see "HTML Output Contract" below) so it renders cleanly in a browser and so downstream tooling (`/plan-phase`, `/create-progress`) can parse it reliably.

## Arguments

Parse `$ARGUMENTS` as follows:
- **First argument:** Path to the source to create the PRD from. This can be either:
  - **A file** — a single document (e.g., a design doc, feature brief, requirements notes, meeting transcript).
  - **A folder** — a directory containing multiple relevant files. All files in the folder (and subfolders) will be read and used as combined source material.
  This argument is required.
- **Second argument (optional):** Output filename for the PRD (default: `docs/prd.html`).

If no arguments are provided, ask the user to specify a source path before proceeding.

## Step 0: Read and Verify Source Material

Before generating the PRD, you **must** read the source material and verify it contains sufficient information to produce a useful PRD.

### Read the Source

Determine whether the first argument is a file or a folder:

- **If it is a file:** Read the file in full. If the file does not exist or cannot be read, inform the user and stop.
- **If it is a folder:** List the folder contents and read all relevant files (e.g., `.md`, `.txt`, `.pdf`, `.docx`, and other document formats). Skip binary files, images, and non-document files. If the folder does not exist, is empty, or contains no readable documents, inform the user and stop. Treat the combined content of all files as the source material for the PRD.

### Sufficiency Check

Evaluate the source document against these criteria. For each, note whether the document provides the information, partially covers it, or is missing it entirely:

| Criteria | Required | Assessment |
|----------|----------|------------|
| **Problem or purpose statement** — What is being built and why? | Yes | ? |
| **Target users or audience** — Who is this for? | Yes | ? |
| **Core functionality or features** — What should it do? | Yes | ? |
| **Technical context or constraints** — Any stack, platform, or integration requirements? | No (but helpful) | ? |
| **Scope boundaries** — What is in/out of scope? | No (but helpful) | ? |
| **Success criteria** — How do we know it worked? | No (but helpful) | ? |

### Verdict

- **Sufficient:** All three required criteria are clearly addressed. Proceed to PRD generation. Report the assessment table to the user and note any gaps you will fill with reasonable assumptions.
- **Partially sufficient:** One or two required criteria are weak or vague. Report the assessment table and ask the user targeted questions to fill the gaps. Do not generate the PRD until the user responds.
- **Insufficient:** The document is missing most required criteria (e.g., it is a raw brainstorm with no clear problem, audience, or feature description). Report the assessment table, explain what is missing, and ask the user to either provide a more complete document or answer the gap questions directly. Do not generate the PRD.

## Output File

Write the PRD to the output filename from the arguments (default: `docs/prd.html`). Create the `docs/` directory if it does not exist.

## HTML Output Contract

The PRD is a **standalone HTML document**. The structure below is also a machine-readable contract: `/create-progress` parses the Implementation Phases section to build the project's phase index, so the phase headings **must** use the exact tag + attribute shape specified.

**Document skeleton** — emit exactly this scaffold, filling `<body>` with the sections defined under "PRD Structure":

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>PRD — <product name></title>
  <style>
    :root { --fg:#1a1a1a; --muted:#666; --accent:#2563eb; --done:#16a34a; --rule:#e5e7eb; }
    body { font:16px/1.6 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif; color:var(--fg); max-width:60rem; margin:2rem auto; padding:0 1.25rem; }
    h1 { font-size:2rem; border-bottom:2px solid var(--rule); padding-bottom:.3rem; }
    h2 { font-size:1.5rem; margin-top:2.5rem; border-bottom:1px solid var(--rule); padding-bottom:.2rem; }
    h3 { font-size:1.2rem; margin-top:1.75rem; }
    code, pre { font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; }
    pre { background:#f6f8fa; padding:1rem; border-radius:6px; overflow:auto; }
    table { border-collapse:collapse; width:100%; margin:1rem 0; }
    th, td { border:1px solid var(--rule); padding:.5rem .75rem; text-align:left; }
    th { background:#f6f8fa; }
    ul.checklist { list-style:none; padding-left:0; }
    ul.checklist > li::before { content:"☐ "; color:var(--muted); }
    ul.checklist > li[data-checked="true"]::before { content:"☑ "; color:var(--done); }
    .meta { color:var(--muted); font-size:.9rem; }
  </style>
</head>
<body>
  <!-- PRD sections go here, in the order defined under "PRD Structure" -->
</body>
</html>
```

**Element conventions:**

- **Sections** — each top-level PRD section is an `<h2>`; subsections are `<h3>`.
- **Checkboxes** — anywhere this template previously used Markdown `[x]` / `[ ]`, use a checklist instead:
  ```html
  <ul class="checklist">
    <li data-checked="true">In-scope / completed item</li>
    <li data-checked="false">Out-of-scope / pending item</li>
  </ul>
  ```
  `data-checked="true"` ⇒ in-scope / done; `data-checked="false"` ⇒ out-of-scope / pending. This is the attribute downstream tooling reads — keep it accurate.
- **Implementation Phases (machine-readable — required shape).** Each phase in the Implementation Phases section MUST be an `<h3>` with this exact shape so `/create-progress` can parse it:
  ```html
  <h3 class="phase" data-phase="1">Phase 1: Foundation</h3>
  ```
  - `data-phase` is the phase number (preserve `0` if you start at Phase 0; sub-phases like `1b` are allowed as `data-phase="1b"`).
  - The visible text is `Phase <N>: <name>`. Do **not** put time estimates inside the heading text — put any estimate in a following `<p class="meta">`. `/create-progress` uses the heading text after the colon, verbatim, as the phase name.
- **Code/examples** — use `<pre><code>…</code></pre>`.
- **Tables** — use real `<table>` markup (the sufficiency assessment, comparison tables, etc.).

## PRD Structure

Create a well-structured PRD with the following sections, each as an `<h2>`. Adapt depth and detail based on available information:

### Required Sections

**1. Executive Summary**
- Concise product overview (2-3 paragraphs)
- Core value proposition
- MVP goal statement

**2. Mission**
- Product mission statement
- Core principles (3-5 key principles)

**3. MVP Scope**
- **In Scope:** Core functionality for MVP (checklist `<li data-checked="true">`)
- **Out of Scope:** Features deferred to future phases (checklist `<li data-checked="false">`)
- Group by categories (Core Functionality, Technical, Integration, Deployment) using `<h3>` subsections

**4. User Stories**
- Primary user stories (5-8 stories) in format: "As a [user], I want to [action], so that [benefit]"
- Include concrete examples for each story
- Add technical user stories if relevant

**5. Core Architecture & Patterns**
- High-level architecture approach
- Directory structure (if applicable)
- Key design patterns and principles
- Technology-specific patterns

**6. Tools/Features**
- Detailed feature specifications
- If building an agent: Tool designs with purpose, operations, and key features
- If building an app: Core feature breakdown

**7. Technology Stack**
- Backend/Frontend technologies with versions
- Dependencies and libraries
- Optional dependencies
- Third-party integrations

**8. Security & Configuration**
- Authentication/authorization approach
- Configuration management (environment variables, settings)
- Security scope (in-scope and out-of-scope)
- Deployment considerations

**9. API Specification** (if applicable)
- Endpoint definitions
- Request/response formats
- Authentication requirements
- Example payloads

**10. Success Criteria**
- MVP success definition
- Functional requirements (checklist with `data-checked`)
- Quality indicators
- User experience goals

**11. Implementation Phases**
- Break down into reasonable phases, each headed by `<h3 class="phase" data-phase="<N>">Phase <N>: <name></h3>` (see HTML Output Contract — this shape is required)
- Each phase includes: Goal, Deliverables (checklist with `data-checked`), Validation criteria
- Realistic timeline estimates in a `<p class="meta">` after the heading (never inside the heading text)

**12. Future Considerations**
- Post-MVP enhancements
- Integration opportunities
- Advanced features for later phases

**13. Risks & Mitigations**
- 3-5 key risks with specific mitigation strategies

**14. Appendix** (if applicable)
- Related documents
- Key dependencies with links
- Repository/project structure

## Instructions

### 1. Extract Requirements
- Review the source document in full
- Identify explicit requirements and implicit needs
- Note technical constraints and preferences
- Capture user goals and success criteria
- Supplement with conversation context if the user has provided additional details

### 2. Synthesize Information
- Organize requirements into appropriate sections
- Fill in reasonable assumptions where details are missing
- Maintain consistency across sections
- Ensure technical feasibility

### 3. Write the PRD
- Use clear, professional language
- Include concrete examples and specifics
- Use the HTML structure and element conventions defined in "HTML Output Contract" (headings, lists, `<pre><code>` blocks, `<table>`, checklists)
- Add code snippets for technical sections where helpful
- Keep Executive Summary concise but comprehensive

### 4. Quality Checks
- [ ] Output is a valid standalone HTML document (doctype, `<head>` with embedded `<style>`, `<body>`)
- [ ] Every Implementation Phases heading uses `<h3 class="phase" data-phase="<N>">Phase <N>: <name></h3>`
- [ ] All required sections present
- [ ] User stories have clear benefits
- [ ] MVP scope is realistic and well-defined
- [ ] Technology choices are justified
- [ ] Implementation phases are actionable
- [ ] Success criteria are measurable
- [ ] Consistent terminology throughout

## Style Guidelines

- **Tone:** Professional, clear, action-oriented
- **Format:** Standalone HTML — use semantic tags (`<h1>`–`<h4>`, `<ul>`/`<ol>`, `<table>`, `<pre><code>`) and the checklist convention
- **Checkboxes:** Use `<li data-checked="true">` for in-scope / completed items, `<li data-checked="false">` for out-of-scope / pending items
- **Specificity:** Prefer concrete examples over abstract descriptions
- **Length:** Comprehensive but scannable (typically 30-60 sections worth of content)

## Output Confirmation

After creating the PRD:
1. Confirm the file path where it was written (e.g., `docs/prd.html`)
2. Provide a brief summary of the PRD contents
3. Highlight any assumptions made due to missing information
4. Suggest next steps (e.g., review, refinement, run `/plan-phase` per phase)

## Notes

- The source material (file or folder) is the primary input — always read and verify it before generating
- If the sufficiency check fails, do not generate a partial PRD; ask for clarification first
- Adapt section depth based on what the source document covers in detail
- For highly technical products, emphasize architecture and technical stack
- For user-facing products, emphasize user stories and experience
- This command contains the complete PRD template structure - no external references needed
- The PRD is consumed by `/plan-phase` (reads scope/dependencies) and `/create-progress` (parses the `data-phase` headings). Keeping the HTML contract intact is what lets that pipeline work unattended.
