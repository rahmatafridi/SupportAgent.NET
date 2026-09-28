import { Link, NavLink, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

export function AppHeader() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  if (!user) return null;
  const customer = user.roles.includes("Customer");
  const MenuLink = customer ? Link : NavLink;
  return (
    <header
      className={`app-header ${customer ? "customer-header" : "app-sidebar"}`}
    >
      <div>
        <strong>{user.displayName}</strong> · {user.organizationName} ·{" "}
        {user.roles.join(", ")}
      </div>
      <nav>
        <MenuLink to={customer ? "/portal" : "/tickets"}>
          {customer ? "My requests" : "Tickets"}
        </MenuLink>
        <MenuLink to={customer ? "/portal/invoices" : "/invoices"}>
          Invoices
        </MenuLink>
        {user.roles.some(
          (role) => role === "Admin" || role === "SupportAgent",
        ) && <MenuLink to="/business-query">Business Intelligence</MenuLink>}
        {user.roles.some(
          (role) => role === "Admin" || role === "SupportAgent",
        ) && <MenuLink to="/knowledge">Knowledge</MenuLink>}
        {user.roles.includes("Admin") && (
          <MenuLink to="/admin/usage">AI usage</MenuLink>
        )}
        {user.roles.includes("Admin") && (
          <MenuLink to="/admin/data-sources">Data sources</MenuLink>
        )}
        {import.meta.env.DEV && user.roles.includes("Admin") && (
          <MenuLink to="/dev/ai-test">AI test</MenuLink>
        )}
        <button
          onClick={async () => {
            await logout();
            navigate("/login");
          }}
        >
          Log out
        </button>
      </nav>
    </header>
  );
}
