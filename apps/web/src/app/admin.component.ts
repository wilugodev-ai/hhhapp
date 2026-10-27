import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  OnInit,
  PendingTasks,
  signal,
  viewChild,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { ApiService, errorMessage } from "./api.service";
import {
  AdminAudit,
  AdminOrganization,
  AdminUser,
  SetupLink,
  UserDirectory,
} from "./admin.models";

@Component({
  selector: "hhh-admin",
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: "./admin.component.html",
})
export class AdminComponent implements OnInit {
  api = inject(ApiService);
  private pending = inject(PendingTasks);
  private injector = inject(Injector);
  private destroyRef = inject(DestroyRef);
  private orgNameInput =
    viewChild<ElementRef<HTMLInputElement>>("orgNameInput");
  private orgEditInput =
    viewChild<ElementRef<HTMLInputElement>>("orgEditInput");
  private inviteNameInput =
    viewChild<ElementRef<HTMLInputElement>>("inviteNameInput");
  private editNameInput =
    viewChild<ElementRef<HTMLInputElement>>("editNameInput");
  private setupLinkInput =
    viewChild<ElementRef<HTMLInputElement>>("setupLinkInput");
  private errorBanner = viewChild<ElementRef<HTMLElement>>("errorBanner");
  private confirmationButton =
    viewChild<ElementRef<HTMLElement>>("confirmationButton");
  isSystem = computed(
    () => this.api.user()?.roles.includes("System Administrator") ?? false,
  );
  allowed = computed(
    () =>
      this.isSystem() ||
      (this.api.user()?.roles.includes("Organization Administrator") ?? false),
  );
  organizations = signal<AdminOrganization[]>([]);
  directory = signal<UserDirectory | null>(null);
  audit = signal<AdminAudit | null>(null);
  loading = signal(false);
  busy = signal(false);
  error = signal("");
  notice = signal("");
  setup = signal<SetupLink | null>(null);
  selectedOrg = "";
  search = "";
  status = "";
  page = 1;
  auditPage = 1;
  selected = computed(() =>
    this.organizations().find((o) => o.id === this.directoryOrg()),
  );
  private directoryOrg = signal("");
  private request = 0;
  inviteOpen = signal(false);
  inviteEmail = "";
  inviteName = "";
  inviteRole = "Executive Viewer";
  editing = signal<AdminUser | null>(null);
  editName = "";
  editRole = "";
  editActive = true;
  action = signal<{ user: AdminUser; operation: string; label: string } | null>(
    null,
  );
  orgCreateOpen = signal(false);
  orgName = "";
  orgAdminEmail = "";
  orgAdminName = "";
  orgIncludeSampleData = true;
  orgEditing = signal(false);
  orgEditName = "";
  orgEditActive = true;
  orgVersion = 0;
  link = computed(() =>
    this.setup()
      ? new URL(this.setup()!.setupPath, window.location.origin).href
      : "",
  );
  roleCatalog = computed(() => this.directory()?.roles ?? []);
  ngOnInit() {
    if (this.allowed()) this.pending.run(() => this.refreshOrganizations());
  }
  async refreshOrganizations() {
    this.loading.set(true);
    this.error.set("");
    try {
      const orgs = await this.api.adminOrganizations();
      this.organizations.set(orgs);
      if (!orgs.some((o) => o.id === this.selectedOrg))
        this.selectedOrg = this.api.user()?.organizationId ?? orgs[0]?.id ?? "";
      if (this.directoryOrg() !== this.selectedOrg) {
        this.directory.set(null);
        this.audit.set(null);
        this.editing.set(null);
        this.action.set(null);
      }
      return this.selectedOrg ? await this.load() : true;
    } catch (e) {
      this.error.set(errorMessage(e));
      return false;
    } finally {
      this.loading.set(false);
    }
  }
  async load(page = this.page, auditPage = this.auditPage) {
    const request = ++this.request;
    this.page = page;
    this.auditPage = auditPage;
    this.loading.set(true);
    this.error.set("");
    const org = this.selectedOrg;
    this.directoryOrg.set(org);
    try {
      const [directory, audit] = await Promise.all([
        this.api.adminUsers(org, this.search, this.status, page),
        this.api.adminAudit(org, auditPage),
      ]);
      if (request === this.request) {
        this.directory.set(directory);
        this.audit.set(audit);
      }
      return request === this.request;
    } catch (e) {
      if (request === this.request) this.error.set(errorMessage(e));
      return false;
    } finally {
      if (request === this.request) this.loading.set(false);
    }
  }
  async reload() {
    if (this.loading() || this.busy()) return;
    this.notice.set("");
    this.editing.set(null);
    this.action.set(null);
    this.orgEditing.set(false);
    if (await this.refreshOrganizations()) {
      this.notice.set(
        "Directory reloaded. People, permissions, and audit activity are up to date.",
      );
    } else {
      this.focusField(this.errorBanner);
    }
  }
  async changeOrganization() {
    this.closeEditors();
    this.inviteName = "";
    this.inviteEmail = "";
    this.inviteRole = "Executive Viewer";
    this.directory.set(null);
    this.audit.set(null);
    this.editing.set(null);
    this.action.set(null);
    this.setup.set(null);
    this.orgEditing.set(false);
    this.notice.set("");
    this.search = "";
    this.status = "";
    await this.load(1, 1);
  }
  canEdit(user: AdminUser) {
    return (
      user.id !== this.api.user()?.id &&
      (this.isSystem() || user.role !== "System Administrator")
    );
  }
  private focusField(field: () => ElementRef<HTMLElement> | undefined) {
    if (this.destroyRef.destroyed) return;
    afterNextRender(() => field()?.nativeElement.focus(), {
      injector: this.injector,
    });
  }
  private closeEditors() {
    this.orgCreateOpen.set(false);
    this.orgEditing.set(false);
    this.inviteOpen.set(false);
    this.editing.set(null);
    this.action.set(null);
  }
  openOrganization() {
    if (!this.isSystem() || this.loading() || this.busy()) return;
    this.closeEditors();
    this.orgCreateOpen.set(true);
    this.focusField(this.orgNameInput);
  }
  openInvite() {
    if (this.loading() || this.busy() || !this.selected()?.isActive) return;
    this.closeEditors();
    this.inviteOpen.set(true);
    this.focusField(this.inviteNameInput);
  }
  edit(user: AdminUser) {
    this.closeEditors();
    this.editing.set(user);
    this.editName = user.displayName;
    this.editRole = user.role;
    this.editActive = user.isActive;
    this.focusField(this.editNameInput);
  }
  async run(work: () => Promise<void>) {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set("");
    this.notice.set("");
    try {
      await work();
    } catch (e) {
      this.error.set(errorMessage(e));
      this.focusField(this.errorBanner);
    } finally {
      this.busy.set(false);
    }
  }
  async invite() {
    await this.run(async () => {
      this.setup.set(
        await this.api.inviteUser(this.selectedOrg, {
          email: this.inviteEmail,
          displayName: this.inviteName,
          role: this.inviteRole,
        }),
      );
      this.inviteEmail = "";
      this.inviteName = "";
      this.inviteOpen.set(false);
      this.notice.set(
        "Account created. Share the setup link privately with the intended user.",
      );
      await this.refreshOrganizations();
      this.focusField(this.setupLinkInput);
    });
  }
  async saveUser() {
    const user = this.editing();
    if (!user) return;
    await this.run(async () => {
      await this.api.updateUser(this.selectedOrg, user.id, {
        displayName: this.editName,
        role: this.editRole,
        isActive: this.editActive,
        expectedVersion: user.version,
      });
      this.editing.set(null);
      this.notice.set("Account updated. Previous sessions have been revoked.");
      await this.refreshOrganizations();
    });
  }
  ask(user: AdminUser, operation: string, label: string) {
    this.closeEditors();
    this.action.set({ user, operation, label });
    this.focusField(this.confirmationButton);
  }
  async confirmAction() {
    const action = this.action();
    if (!action) return;
    await this.run(async () => {
      const result = await this.api.adminUserAction(
        this.selectedOrg,
        action.user.id,
        action.operation,
        action.user.version,
      );
      if ("setupPath" in result) this.setup.set(result);
      this.action.set(null);
      this.notice.set(
        action.label + " completed. Previous sessions have been revoked.",
      );
      await this.refreshOrganizations();
      if ("setupPath" in result) this.focusField(this.setupLinkInput);
    });
  }
  async copyLink() {
    try {
      await navigator.clipboard.writeText(this.link());
      this.notice.set(
        "Setup link copied. Share it privately with the intended user.",
      );
    } catch {
      this.notice.set("Select and copy the setup link below.");
      this.focusField(this.setupLinkInput);
    }
  }
  editOrganization() {
    const org = this.selected();
    if (!org) return;
    this.closeEditors();
    this.orgEditName = org.name;
    this.orgEditActive = org.isActive;
    this.orgVersion = org.version;
    this.orgEditing.set(true);
    this.focusField(this.orgEditInput);
  }
  async saveOrganization() {
    await this.run(async () => {
      await this.api.updateOrganization(this.selectedOrg, {
        name: this.orgEditName,
        isActive: this.orgEditActive,
        expectedVersion: this.orgVersion,
      });
      this.orgEditing.set(false);
      this.notice.set("Organization updated.");
      await this.refreshOrganizations();
    });
  }
  async createOrganization() {
    await this.run(async () => {
      const result = await this.api.createOrganization({
        name: this.orgName,
        adminEmail: this.orgAdminEmail,
        adminName: this.orgAdminName,
        includeSampleData:
          this.api.sampleDataEnabled() && this.orgIncludeSampleData,
      });
      this.selectedOrg = result.organizationId;
      this.setup.set(result.invitation);
      this.search = "";
      this.status = "";
      this.page = 1;
      this.auditPage = 1;
      this.orgName = "";
      this.orgAdminEmail = "";
      this.orgAdminName = "";
      this.orgCreateOpen.set(false);
      this.notice.set(
        "Organization created. Share the first administrator's setup link privately.",
      );
      await this.refreshOrganizations();
      this.focusField(this.setupLinkInput);
    });
  }
  statusLabel(user: AdminUser) {
    return !user.isActive
      ? "Inactive"
      : user.invitationPending
        ? "Setup pending"
        : "Active";
  }
  locked(user: AdminUser) {
    return (
      !!user.lockoutEnd && new Date(user.lockoutEnd).getTime() > Date.now()
    );
  }
  settingsRole(role: string) {
    return [
      "System Administrator",
      "Organization Administrator",
      "Chief Financial Officer",
    ].includes(role);
  }
  adminRole(role: string) {
    return ["System Administrator", "Organization Administrator"].includes(
      role,
    );
  }
  eventLabel(action: string) {
    return action.replace(/^admin\./, "").replaceAll(".", " ");
  }
}
