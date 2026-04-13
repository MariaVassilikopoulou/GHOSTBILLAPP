import { useState } from "react";

const PREV_KEY = "ghostbill_prev_ghosts";

export function usePrevGhosts() {
  const [prevNames] = useState<Set<string>>(() => {
    try {
      const stored = localStorage.getItem(PREV_KEY);
      return stored ? new Set(JSON.parse(stored) as string[]) : new Set();
    } catch {
      return new Set();
    }
  });

  function saveCurrentGhosts(names: string[]) {
    try {
      localStorage.setItem(PREV_KEY, JSON.stringify(names));
    } catch {}
  }

  return { prevNames, saveCurrentGhosts };
}
