import { avatarCls } from "../lib/derive";
import type { User } from "../lib/types";

interface Props {
  user: Pick<User, "id" | "initials" | "name"> | undefined;
  size?: "sm" | "md" | "lg";
}

export function Avatar({ user, size = "md" }: Props) {
  if (!user) return null;
  return (
    <span
      className={`avatar ${avatarCls(user.id)} ${size}`}
      title={user.name}
      aria-hidden="true"
    >
      {user.initials}
    </span>
  );
}
