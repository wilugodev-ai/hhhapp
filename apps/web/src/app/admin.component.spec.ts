import { TestBed } from "@angular/core/testing";
import { HttpErrorResponse } from "@angular/common/http";
import { signal } from "@angular/core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AdminComponent } from "./admin.component";
import { ApiService } from "./api.service";
import { AdminUser, SetupLink } from "./admin.models";
import { User } from "./models";
import { provideRouter } from "@angular/router";

const person: AdminUser = {
  id: "person-1",
  email: "person@example.com",
  displayName: "Taylor Jones",
  role: "Executive Viewer",
  isActive: true,
  invitationPending: false,
  invitationExpiresAt: null,
  lockoutEnd: null,
  version: 3,
};
const setup: SetupLink = {
  user: person,
  setupPath: "/activate#user=person-1&token=private-token",
  expiresAt: "2026-10-29T12:00:00Z",
};
const roles = [
  "Organization Administrator",
  "Chief Financial Officer",
  "Financial Analyst",
  "Executive Viewer",
];
async function render(role = "Organization Administrator") {
  const api = {
    sampleDataEnabled: signal(false),
    user: signal<User>({
      id: "current",
      organizationId: "org-1",
      displayName: "Current Admin",
      email: "admin@example.com",
      organization: "Horizon",
      roles: [role],
      mlrTarget: 85,
    }),
    adminOrganizations: vi.fn(async () => [
      {
        id: "org-1",
        name: "Horizon",
        isActive: true,
        version: 1,
        userCount: 2,
        activeUsers: 2,
      },
      {
        id: "org-2",
        name: "Summit",
        isActive: true,
        version: 1,
        userCount: 1,
        activeUsers: 1,
      },
    ]),
    adminUsers: vi.fn(async () => ({
      total: 2,
      page: 1,
      pageSize: 25,
      roles:
        role === "System Administrator"
          ? [...roles, "System Administrator"]
          : roles,
      users: [
        { ...person },
        {
          ...person,
          id: "current",
          displayName: "Current Admin",
          role: "Organization Administrator",
        },
      ],
    })),
    adminAudit: vi.fn(async () => ({
      total: 1,
      page: 1,
      pageSize: 25,
      items: [
        {
          id: 1,
          action: "admin.user.updated",
          occurredAt: "2026-10-27T12:00:00Z",
          actor: "Current Admin",
          detailsJson: '{"subjectId":"person-1"}',
        },
      ],
    })),
    inviteUser: vi.fn(async () => setup),
    updateUser: vi.fn(async () => person),
    adminUserAction: vi.fn(async () => setup),
    createOrganization: vi.fn(),
    updateOrganization: vi.fn(),
  };
  await TestBed.configureTestingModule({
    imports: [AdminComponent],
    providers: [provideRouter([]), { provide: ApiService, useValue: api }],
  }).compileComponents();
  const fixture = TestBed.createComponent(AdminComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const button = (name: string) =>
    [...root.querySelectorAll<HTMLButtonElement>("button")].find(
      (b) => b.textContent?.trim() === name,
    )!;
  return { api, fixture, root, component: fixture.componentInstance, button };
}
afterEach(() => TestBed.resetTestingModule());
function fill(root: HTMLElement, name: string, value: string) {
  const input = root.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;
  input.value = value;
  input.dispatchEvent(new Event("input", { bubbles: true }));
}
describe("Administration workspace", () => {
  it("opens and focuses Invite user beside the toolbar and submits the form", async () => {
    const { api, fixture, root, button } = await render();
    button("Invite user").click();
    await fixture.whenStable();
    const editor = root.querySelector("#admin-invite-user");
    expect(root.querySelector(".admin-toolbar")?.nextElementSibling).toBe(
      editor,
    );
    expect(document.activeElement).toBe(
      root.querySelector('input[name="inviteName"]'),
    );
    fill(root, "inviteName", "Taylor Jones");
    fill(root, "inviteEmail", "person@example.com");
    await fixture.whenStable();
    button("Create setup link").click();
    await vi.waitFor(() =>
      expect(api.inviteUser).toHaveBeenCalledWith("org-1", {
        email: "person@example.com",
        displayName: "Taylor Jones",
        role: "Executive Viewer",
      }),
    );
    await fixture.whenStable();
    const link = root.querySelector<HTMLInputElement>(
      'input[aria-label="Setup link"]',
    );
    expect(link?.value).toContain("private-token");
    expect(document.activeElement).toBe(link);
    expect(root.querySelector("#admin-invite-user")).toBeNull();
  });
  it("opens New organization beside the toolbar and creates its first admin setup link", async () => {
    const { api, fixture, root, button } = await render("System Administrator");
    api.createOrganization.mockResolvedValueOnce({
      organizationId: "org-2",
      invitation: setup,
    });
    button("Invite user").click();
    await fixture.whenStable();
    button("New organization").click();
    await fixture.whenStable();
    expect(root.querySelector("#admin-invite-user")).toBeNull();
    const editor = root.querySelector("#admin-create-organization");
    expect(root.querySelector(".admin-toolbar")?.nextElementSibling).toBe(
      editor,
    );
    expect(document.activeElement).toBe(
      root.querySelector('input[name="orgName"]'),
    );
    fill(root, "orgName", "Summit");
    fill(root, "orgAdminName", "Taylor Jones");
    fill(root, "orgAdminEmail", "person@example.com");
    await fixture.whenStable();
    button("Create organization").click();
    await vi.waitFor(() =>
      expect(api.createOrganization).toHaveBeenCalledWith({
        name: "Summit",
        adminName: "Taylor Jones",
        adminEmail: "person@example.com",
        includeSampleData: false,
      }),
    );
    await fixture.whenStable();
    expect(api.adminUsers).toHaveBeenLastCalledWith("org-2", "", "", 1);
    expect(root.querySelector("#admin-create-organization")).toBeNull();
    expect(document.activeElement).toBe(
      root.querySelector('input[aria-label="Setup link"]'),
    );
    expect(root.querySelector('[role="status"]')?.textContent).toContain(
      "Organization created",
    );
  });
  it("shows loading and completion when Reload directory is clicked", async () => {
    const { api, fixture, root, component, button } = await render();
    const directory = component.directory()!;
    let finish!: (value: typeof directory) => void;
    api.adminUsers.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    button("Reload directory").click();
    fixture.detectChanges();
    expect(button("Loading directory...").disabled).toBe(true);
    expect(button("Invite user").disabled).toBe(true);
    await vi.waitFor(() => expect(api.adminUsers).toHaveBeenCalledTimes(2));
    finish(directory);
    await vi.waitFor(() => expect(component.loading()).toBe(false));
    await fixture.whenStable();
    expect(api.adminAudit).toHaveBeenCalledTimes(2);
    expect(root.querySelector('[role="status"]')?.textContent).toContain(
      "Directory reloaded",
    );
    expect(button("Reload directory").disabled).toBe(false);
  });
  it("shows and focuses a reload error without reporting success", async () => {
    const { api, fixture, root, component, button } = await render();
    api.adminUsers.mockRejectedValueOnce(
      new HttpErrorResponse({ status: 503 }),
    );
    button("Reload directory").click();
    await vi.waitFor(() => expect(component.error()).toContain("HHH API"));
    await fixture.whenStable();
    expect(document.activeElement).toBe(root.querySelector('[role="alert"]'));
    expect(root.querySelector('[role="status"]')).toBeNull();
    expect(button("Reload directory").disabled).toBe(false);
  });
  it("keeps an invitation draft visible when account creation fails", async () => {
    const { api, fixture, root, component, button } = await render();
    api.inviteUser.mockRejectedValueOnce(
      new HttpErrorResponse({
        status: 409,
        error: { title: "This email already has an account." },
      }),
    );
    button("Invite user").click();
    await fixture.whenStable();
    fill(root, "inviteName", "Taylor Jones");
    fill(root, "inviteEmail", "person@example.com");
    await fixture.whenStable();
    button("Create setup link").click();
    await vi.waitFor(() =>
      expect(component.error()).toContain("already has an account"),
    );
    await fixture.whenStable();
    expect(
      root.querySelector<HTMLInputElement>('input[name="inviteEmail"]')?.value,
    ).toBe("person@example.com");
    expect(document.activeElement).toBe(root.querySelector('[role="alert"]'));
    expect(root.querySelector('input[aria-label="Setup link"]')).toBeNull();
    expect(button("Create setup link").disabled).toBe(false);
  });
  it("denies non-admin roles without requesting the directory", async () => {
    const { api, root } = await render("Chief Financial Officer");
    expect(root.textContent).toContain("Administrator access required");
    expect(api.adminOrganizations).not.toHaveBeenCalled();
  });
  it("shows users and audit activity and protects the current account", async () => {
    const { root, button, component } = await render();
    expect(root.textContent).toContain("Taylor Jones");
    expect(root.textContent).toContain("Current Admin");
    expect(root.textContent).toContain(
      "Your account: another admin manages changes",
    );
    expect(component.canEdit({ ...person, role: "System Administrator" })).toBe(
      false,
    );
    expect(button("New organization")).toBeUndefined();
    expect(
      root.querySelector('option[value="System Administrator"]'),
    ).toBeNull();
  });
  it("saves role and activation changes with the displayed version", async () => {
    const { api, component, button, fixture } = await render();
    button("Edit").click();
    fixture.detectChanges();
    await fixture.whenStable();
    component.editName = "Updated Name";
    component.editRole = "Financial Analyst";
    component.editActive = false;
    await component.saveUser();
    expect(api.updateUser).toHaveBeenCalledWith("org-1", "person-1", {
      displayName: "Updated Name",
      role: "Financial Analyst",
      isActive: false,
      expectedVersion: 3,
    });
    expect(component.editing()).toBeNull();
    expect(component.notice()).toContain("sessions have been revoked");
  });
  it("preserves a draft after a concurrent change", async () => {
    const { api, component } = await render();
    api.updateUser.mockRejectedValueOnce(
      new HttpErrorResponse({
        status: 409,
        error: {
          title: "This account changed. Reload the directory before saving.",
        },
      }),
    );
    component.edit(person);
    component.editName = "Unsaved Draft";
    await component.saveUser();
    expect(component.editing()?.id).toBe(person.id);
    expect(component.editName).toBe("Unsaved Draft");
    expect(component.error()).toContain("Reload");
  });
  it("creates an invitation and exposes its link only after success", async () => {
    const { api, component, fixture, root } = await render();
    expect(root.textContent).not.toContain("private-token");
    component.inviteEmail = "person@example.com";
    component.inviteName = "Taylor Jones";
    await component.invite();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(api.inviteUser).toHaveBeenCalledWith("org-1", {
      email: "person@example.com",
      displayName: "Taylor Jones",
      role: "Executive Viewer",
    });
    expect(
      root.querySelector<HTMLInputElement>('input[aria-label="Setup link"]')
        ?.value,
    ).toContain("/activate#user=person-1&token=private-token");
  });
  it("requires a deliberate confirmation before recovery", async () => {
    const { api, component, button, fixture, root } = await render();
    button("Password recovery").click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(root.textContent).toContain("blocks sign-in until");
    expect(api.adminUserAction).not.toHaveBeenCalled();
    await component.confirmAction();
    expect(api.adminUserAction).toHaveBeenCalledWith(
      "org-1",
      "person-1",
      "recovery",
      3,
    );
    expect(component.setup()?.setupPath).toContain("private-token");
  });
  it("clears old organization drafts and links when a system admin switches scope", async () => {
    const { api, component, button, fixture } = await render(
      "System Administrator",
    );
    expect(button("New organization")).toBeDefined();
    component.edit(person);
    component.setup.set(setup);
    component.selectedOrg = "org-2";
    await component.changeOrganization();
    fixture.detectChanges();
    expect(api.adminUsers).toHaveBeenLastCalledWith("org-2", "", "", 1);
    expect(component.editing()).toBeNull();
    expect(component.setup()).toBeNull();
  });
});
