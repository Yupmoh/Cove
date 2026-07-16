export interface LaunchProfileListItem {
  slug: string;
  name: string;
  adapter: string;
  isDefault: boolean;
  model: string | null;
  effort: string | null;
  argCount: number;
  envCount: number;
}

export interface LaunchProfileListResult {
  profiles: LaunchProfileListItem[];
}

export interface LaunchProfileDetail {
  slug: string;
  name: string;
  adapter: string;
  isDefault: boolean;
  model: string | null;
  effort: string | null;
  cliArgs: string[];
  env: Record<string, string>;
  permissions: Record<string, boolean>;
  skills: string[];
  agent: string | null;
  schemaVersion: number;
}

export interface LauncherOptionChoice {
  value: string;
  label: string | null;
}

export interface LauncherOption {
  key: string;
  label: string;
  type: string;
  defaultValueRaw: string | null;
  choices: LauncherOptionChoice[] | null;
}

export interface LauncherSuggestedFlag {
  flag: string;
  description: string | null;
  values: string[] | null;
}

export interface LauncherOptionsResponse {
  options: LauncherOption[];
  suggestedFlags: LauncherSuggestedFlag[];
}

export interface CreateProfileInput {
  adapter: string;
  slug: string;
  name: string;
  model?: string | null;
  effort?: string | null;
  cliArgs?: string[];
  env?: Record<string, string>;
  agent?: string | null;
  isDefault?: boolean;
}

export interface UpdateProfileInput {
  adapter: string;
  slug: string;
  name?: string;
  model?: string | null;
  effort?: string | null;
  cliArgs?: string[];
  env?: Record<string, string>;
  agent?: string | null;
  isDefault?: boolean;
}

const SLUG_RE = /^[a-z0-9-]{1,64}$/;

export function isValidProfileSlug(slug: string): boolean {
  return SLUG_RE.test(slug);
}

export function deriveProfileSlug(text: string): string {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 64)
    .replace(/-+$/, "");
}

export function profileDisplayName(profile: LaunchProfileListItem): string {
  return profile.name || profile.slug;
}

export function profilePickerLabel(profile: LaunchProfileListItem): string {
  const star = profile.isDefault ? "★ " : "";
  const model = profile.model ? ` · ${profile.model}` : "";
  return `${star}${profileDisplayName(profile)}${model}`;
}

export function isFreeformModelOption(option: LauncherOption): boolean {
  return option.type === "text";
}

export function modelChoicesWithFreeform(
  choices: LauncherOptionChoice[] | null,
  currentValue: string | null,
): LauncherOptionChoice[] {
  const base = choices ?? [];
  if (currentValue && !base.some((c) => c.value === currentValue)) {
    return [...base, { value: currentValue, label: currentValue }];
  }
  return base;
}

export function envRowsFromMap(env: Record<string, string>): Array<{ key: string; value: string }> {
  return Object.entries(env).map(([key, value]) => ({ key, value }));
}

export function envMapFromRows(rows: Array<{ key: string; value: string }>): Record<string, string> {
  const env: Record<string, string> = {};
  for (const row of rows) {
    const key = row.key.trim();
    if (key) env[key] = row.value;
  }
  return env;
}

export function cliArgsFromRows(rows: Array<{ flag: string; value: string | null }>): string[] {
  const args: string[] = [];
  for (const row of rows) {
    const flag = row.flag.trim();
    if (!flag) continue;
    args.push(row.value ? `${flag}=${row.value}` : flag);
  }
  return args;
}

export function firstDefault(profiles: LaunchProfileListItem[]): LaunchProfileListItem | null {
  return profiles.find((p) => p.isDefault) ?? profiles[0] ?? null;
}
