# RDLC MCP Server

A [Model Context Protocol](https://modelcontextprotocol.io) server that lets an AI assistant read
and edit Microsoft RDLC report definitions (`.rdl` and `.rdlc`).

## Running it

```bash
dnx ReportExpert.Rdl.Mcp -y
```

Or point a client at the built executable directly:

```json
{
  "servers": {
    "rdlc": {
      "type": "stdio",
      "command": "rdlc-mcp"
    }
  }
}
```

Every tool takes a `filepath` as its first argument, so no working directory needs to be configured.

## Tools

Thirty-one tools, grouped by what they touch. Ten are read-only and safe to call without asking;
the other twenty-one write to the file and are marked destructive so a host can prompt first.

### Inspecting a report

| Tool | What it does |
| --- | --- |
| `describe_rdl_report` | Dataset, parameter and column counts, plus the query behind each dataset. The place to start. |
| `get_rdl_datasets` | Datasets with their data source, command and query parameters. Fields on request via `field_limit`. |
| `get_rdl_parameters` | Report parameters with their type, prompt, defaults and allowed values. |
| `get_rdl_columns` | Columns of a table with their index, header, width, binding and format. |
| `get_rdl_tablixes` | Every table in the report, with the dataset it is bound to and its grid size. |
| `get_rdl_page_setup` | Page size, orientation, margins and the usable width between them. |
| `get_rdl_report_items` | Textboxes, images, rectangles, lines, subreports, tables and charts, with positions and sizes. |
| `find_field_usages` | Every expression referencing a dataset field. Run before renaming or removing one. |
| `validate_rdl` | Structural and field-reference problems that would stop the report rendering. |

### Editing columns

| Tool | What it does |
| --- | --- |
| `update_column_header` | Rename a header by matching its current text exactly. |
| `update_column_width` | Set a column's width and recalculate the table total. |
| `update_column_format` | Set the detail cell's format string, such as `C2` or `dd/MM/yyyy`. |
| `add_column` | Insert a column, creating a cell in every row. Pass `column_index: -1` to append. |
| `remove_column` | Remove a column and, by default, shrink the page to fit what remains. |
| `move_column` | Reorder a column, carrying its width, header, binding and styling. |
| `set_column_style` | Font, colour and alignment, applied to the header, detail, totals row or all three. |

### Editing datasets

| Tool | What it does |
| --- | --- |
| `update_stored_procedure` | Point a dataset at a different stored procedure or query. |
| `add_dataset_field` | Declare a field so expressions can reference it. |
| `remove_dataset_field` | Remove a field declaration. |
| `rename_dataset_field` | Rename a field and rewrite the expressions that reference it. |
| `add_dataset` | Add an empty dataset. |
| `remove_dataset` | Remove a dataset, refusing while a table is still bound to it. |

### Editing parameters

| Tool | What it does |
| --- | --- |
| `add_parameter` | Add a report parameter. |
| `update_parameter` | Change a parameter's prompt or default value. |
| `remove_parameter` | Remove a parameter, refusing while it is still referenced. |

### Editing layout

| Tool | What it does |
| --- | --- |
| `set_page_setup` | Page size, orientation and margins. |
| `set_textbox_value` | Set the text or expression a named textbox displays. |
| `set_report_item_visibility` | Show or hide an image, textbox, table or other named item. |
| `remove_report_item` | Remove a named item from the layout (not from inside a table cell). |

### Backups

| Tool | What it does |
| --- | --- |
| `list_rdl_backups` | The automatic backups of a report, newest first. |
| `restore_rdl_backup` | Roll a report back to a backup. The current content is backed up first. |

## Safety

Four things make agent-driven edits recoverable:

- **`dry_run`.** Every mutating tool except `restore_rdl_backup` accepts `dry_run: true`, which
  applies the edit in memory and returns a unified diff without touching the file.
- **Automatic backups.** A timestamped copy is written to a `.rdlc-backups` folder beside the report
  before each write. The twenty most recent are kept.
- **Re-validation.** An edit that would turn a valid report into an invalid one is refused and
  nothing is written. A report that was already invalid stays editable, so it can be repaired.
- **Atomic writes.** The new content is staged in a sibling file and moved into place, so an
  interrupted write cannot leave a half-written report on disk. Access to a given report is
  serialized by a named mutex, across processes as well as within one.

## Response format

Every tool returns the same envelope:

```json
{ "ok": true, "data": { } }
```

```json
{
  "ok": false,
  "error": { "code": "not_found", "message": "...", "hint": "Call get_rdl_datasets to ..." }
}
```

Check `ok` first. On failure, `error.hint` always says what to do next.

The error codes are `file_not_found`, `invalid_path`, `file_locked`, `invalid_rdl`, `not_found`,
`invalid_argument`, `refused`, `validation_failed`, `resource_limit` and `internal_error`.

## Configuration

| Environment variable | Effect |
| --- | --- |
| `RDLC_MCP_ALLOWED_ROOTS` | Path-separated list of directories the server may open. Unset means no restriction. |
| `RDLC_MCP_BACKUPS` | Set to `false` to disable automatic backups. |
| `RDLC_MCP_BACKUP_RETENTION` | How many backups to keep per report. Defaults to 20. |

Logging goes to stderr, because stdout carries the protocol.
