import { useEffect, useState } from 'react';
import {
  makeStyles,
  tokens,
  Body1,
  Subtitle2,
  Caption1,
  Spinner,
  MessageBar,
  MessageBarBody,
  Button,
  Input,
  Field,
  Dialog,
  DialogTrigger,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
} from '@fluentui/react-components';
import { AddRegular, ArrowResetRegular, DeleteRegular, CopyRegular, CheckmarkRegular } from '@fluentui/react-icons';
import {
  listAgents,
  createAgent,
  rotateAgent,
  revokeAgent,
  type AgentSummary,
  type CreateAgentResponse,
  type Principal,
} from '../api';

const useStyles = makeStyles({
  toolbar: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: '16px',
  },
  table: {
    width: '100%',
    borderCollapse: 'collapse',
    background: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusLarge,
    overflow: 'hidden',
    border: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  th: {
    textAlign: 'left',
    padding: '10px 14px',
    fontSize: '12px',
    fontWeight: 600,
    textTransform: 'uppercase',
    letterSpacing: '0.5px',
    color: tokens.colorNeutralForeground3,
    background: tokens.colorNeutralBackground3,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  td: {
    padding: '12px 14px',
    fontSize: '13px',
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  mono: {
    fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
  },
  status: {
    fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
    fontSize: '12px',
    padding: '2px 8px',
    borderRadius: tokens.borderRadiusCircular,
  },
  active: {
    background: tokens.colorPaletteGreenBackground2,
    color: tokens.colorPaletteGreenForeground2,
  },
  revoked: {
    background: tokens.colorPaletteRedBackground2,
    color: tokens.colorPaletteRedForeground2,
  },
  keyDisplay: {
    background: tokens.colorNeutralBackground3,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    padding: '12px',
    borderRadius: tokens.borderRadiusMedium,
    fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
    wordBreak: 'break-all',
    fontSize: '13px',
    flex: 1,
    minWidth: 0,
  },
  keyRow: {
    display: 'flex',
    alignItems: 'stretch',
    gap: '8px',
    marginTop: '8px',
  },
  installPanel: {
    marginTop: '16px',
    background: tokens.colorNeutralBackground3,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    padding: '12px',
  },
  installHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: '8px',
    marginBottom: '8px',
    flexWrap: 'wrap',
  },
  osTabs: {
    display: 'flex',
    gap: '4px',
  },
  osTab: {
    padding: '4px 10px',
    fontSize: '12px',
    borderRadius: tokens.borderRadiusSmall,
    borderTopWidth: '1px',
    borderRightWidth: '1px',
    borderBottomWidth: '1px',
    borderLeftWidth: '1px',
    borderTopStyle: 'solid',
    borderRightStyle: 'solid',
    borderBottomStyle: 'solid',
    borderLeftStyle: 'solid',
    borderTopColor: tokens.colorNeutralStroke2,
    borderRightColor: tokens.colorNeutralStroke2,
    borderBottomColor: tokens.colorNeutralStroke2,
    borderLeftColor: tokens.colorNeutralStroke2,
    background: 'transparent',
    color: tokens.colorNeutralForeground2,
    cursor: 'pointer',
    fontFamily: 'inherit',
  },
  osTabActive: {
    background: tokens.colorBrandBackground2,
    color: tokens.colorBrandForeground2,
    borderTopColor: tokens.colorBrandStroke2,
    borderRightColor: tokens.colorBrandStroke2,
    borderBottomColor: tokens.colorBrandStroke2,
    borderLeftColor: tokens.colorBrandStroke2,
  },
  installSnippet: {
    background: tokens.colorNeutralBackground1,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusSmall,
    padding: '10px 12px',
    fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
    fontSize: '12px',
    whiteSpace: 'pre-wrap',
    wordBreak: 'break-all',
    marginTop: '4px',
  },
  snippetRow: {
    display: 'flex',
    alignItems: 'stretch',
    gap: '8px',
  },
  snippetCol: {
    flex: 1,
    minWidth: 0,
  },
});

type Os = 'windows' | 'unix';

function detectOs(): Os {
  if (typeof navigator === 'undefined') return 'unix';
  const ua = navigator.userAgent.toLowerCase();
  if (ua.includes('windows') || ua.includes('win32') || ua.includes('win64')) return 'windows';
  return 'unix';
}

function buildSnippet(os: Os, baseUrl: string, agentKey: string): string {
  if (os === 'windows') {
    return [
      `$env:MINIONTANK_AGENT_KEY = "${agentKey}"`,
      `$env:MINIONTANK_BASE_URL = "${baseUrl}"`,
      `irm "${baseUrl}/install.ps1" | iex`,
    ].join('; ');
  }
  return `MINIONTANK_AGENT_KEY="${agentKey}" MINIONTANK_BASE_URL="${baseUrl}" sh -c "$(curl -fsSL ${baseUrl}/install.sh)"`;
}

export function Agents({ principal }: { principal: Principal | null }) {
  const styles = useStyles();
  const [agents, setAgents] = useState<AgentSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [newName, setNewName] = useState('');
  const [revealedKey, setRevealedKey] = useState<CreateAgentResponse | null>(null);
  const [keyCopied, setKeyCopied] = useState(false);
  const [snippetCopied, setSnippetCopied] = useState(false);
  const [os, setOs] = useState<Os>(detectOs());

  const isHuman = principal?.kind === 'Human' || principal?.kind === 'Dev';

  const refresh = () =>
    listAgents()
      .then(setAgents)
      .catch((e) => setError(e.message ?? String(e)));

  useEffect(() => {
    if (isHuman) {
      refresh();
    }
  }, [isHuman]);

  if (!isHuman) {
    return (
      <MessageBar intent="info">
        <MessageBarBody>
          Sign in with your Microsoft account to manage agents. Use the <strong>Sign in</strong> button in the header.
        </MessageBarBody>
      </MessageBar>
    );
  }

  const onCreate = async () => {
    if (!newName.trim()) return;
    try {
      const res = await createAgent(newName.trim());
      setRevealedKey(res);
      setKeyCopied(false);
      setSnippetCopied(false);
      setNewName('');
      await refresh();
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const onRotate = async (agentId: string) => {
    try {
      const res = await rotateAgent(agentId);
      setRevealedKey(res);
      setKeyCopied(false);
      setSnippetCopied(false);
      await refresh();
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const onCopyKey = async () => {
    if (!revealedKey) return;
    try {
      await navigator.clipboard.writeText(revealedKey.apiKey);
      setKeyCopied(true);
      setTimeout(() => setKeyCopied(false), 2000);
    } catch (e) {
      setError(`Couldn't copy to clipboard: ${(e as Error).message}`);
    }
  };

  const onCopySnippet = async () => {
    if (!revealedKey) return;
    try {
      await navigator.clipboard.writeText(buildSnippet(os, window.location.origin, revealedKey.apiKey));
      setSnippetCopied(true);
      setTimeout(() => setSnippetCopied(false), 2000);
    } catch (e) {
      setError(`Couldn't copy to clipboard: ${(e as Error).message}`);
    }
  };

  const onRevoke = async (agentId: string) => {
    if (!confirm(`Revoke agent ${agentId}? This is irreversible.`)) return;
    try {
      await revokeAgent(agentId);
      await refresh();
    } catch (e) {
      setError((e as Error).message);
    }
  };

  return (
    <div>
      <div className={styles.toolbar}>
        <Subtitle2>Agents</Subtitle2>
        <Dialog>
          <DialogTrigger disableButtonEnhancement>
            <Button appearance="primary" icon={<AddRegular />}>New agent</Button>
          </DialogTrigger>
          <DialogSurface>
            <DialogBody>
              <DialogTitle>Create agent</DialogTitle>
              <DialogContent>
                <Field label="Display name">
                  <Input
                    value={newName}
                    onChange={(_, d) => setNewName(d.value)}
                    placeholder="e.g. my-agent"
                  />
                </Field>
              </DialogContent>
              <DialogActions>
                <DialogTrigger disableButtonEnhancement>
                  <Button appearance="secondary">Cancel</Button>
                </DialogTrigger>
                <DialogTrigger disableButtonEnhancement>
                  <Button appearance="primary" onClick={onCreate}>Create</Button>
                </DialogTrigger>
              </DialogActions>
            </DialogBody>
          </DialogSurface>
        </Dialog>
      </div>

      {error && (
        <MessageBar intent="error" style={{ marginBottom: '12px' }}>
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      )}

      {revealedKey && (
        <MessageBar intent="warning" style={{ marginBottom: '12px' }}>
          <MessageBarBody>
            <strong>Save this key now — it will not be shown again.</strong>
            <div className={styles.keyRow}>
              <div className={styles.keyDisplay}>
                {revealedKey.apiKey}
              </div>
              <Button
                appearance="secondary"
                icon={keyCopied ? <CheckmarkRegular /> : <CopyRegular />}
                onClick={onCopyKey}
              >
                {keyCopied ? 'Copied' : 'Copy key'}
              </Button>
            </div>
            <Caption1 style={{ display: 'block', marginTop: '6px' }}>
              Agent: {revealedKey.displayName} ({revealedKey.agentId}) — expires {new Date(revealedKey.expiresAt).toLocaleDateString()}
            </Caption1>

            <div className={styles.installPanel}>
              <div className={styles.installHeader}>
                <Body1><strong>One-line install</strong> — pastes this key + base URL, then installs the skill.</Body1>
                <div className={styles.osTabs} role="tablist" aria-label="Choose OS">
                  <button
                    type="button"
                    role="tab"
                    aria-selected={os === 'windows'}
                    className={`${styles.osTab} ${os === 'windows' ? styles.osTabActive : ''}`}
                    onClick={() => setOs('windows')}
                  >
                    Windows (PowerShell)
                  </button>
                  <button
                    type="button"
                    role="tab"
                    aria-selected={os === 'unix'}
                    className={`${styles.osTab} ${os === 'unix' ? styles.osTabActive : ''}`}
                    onClick={() => setOs('unix')}
                  >
                    macOS / Linux
                  </button>
                </div>
              </div>
              <div className={styles.snippetRow}>
                <div className={styles.snippetCol}>
                  <div className={styles.installSnippet}>
                    {buildSnippet(os, window.location.origin, revealedKey.apiKey)}
                  </div>
                </div>
                <Button
                  appearance="secondary"
                  icon={snippetCopied ? <CheckmarkRegular /> : <CopyRegular />}
                  onClick={onCopySnippet}
                >
                  {snippetCopied ? 'Copied' : 'Copy command'}
                </Button>
              </div>
              <Caption1 style={{ display: 'block', marginTop: '6px' }}>
                Paste this into your shell. Your key is included inline, so it'll briefly land in shell history — clear
                history if that matters to you. Restart your shell / Copilot CLI session after running.
              </Caption1>
            </div>

            <Button appearance="primary" onClick={() => setRevealedKey(null)} style={{ marginTop: '12px' }}>
              I've saved it
            </Button>
          </MessageBarBody>
        </MessageBar>
      )}

      {agents === null ? (
        <Spinner />
      ) : agents.length === 0 ? (
        <Body1>No agents yet.</Body1>
      ) : (
        <table className={styles.table}>
          <thead>
            <tr>
              <th className={styles.th}>Name</th>
              <th className={styles.th}>Agent ID</th>
              <th className={styles.th}>Created by</th>
              <th className={styles.th}>Status</th>
              <th className={styles.th}>Last 4</th>
              <th className={styles.th}>Expires</th>
              <th className={styles.th}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {agents.map((a) => (
              <tr key={a.agentId}>
                <td className={styles.td}>{a.displayName}</td>
                <td className={`${styles.td} ${styles.mono}`}>{a.agentId.slice(0, 16)}…</td>
                <td className={styles.td}>{a.createdBy}</td>
                <td className={styles.td}>
                  <span className={`${styles.status} ${a.status === 'active' ? styles.active : styles.revoked}`}>
                    {a.status}
                  </span>
                </td>
                <td className={`${styles.td} ${styles.mono}`}>…{a.lastFour}</td>
                <td className={styles.td}>{new Date(a.expiresAt).toLocaleDateString()}</td>
                <td className={styles.td}>
                  <Button
                    appearance="subtle"
                    icon={<ArrowResetRegular />}
                    size="small"
                    onClick={() => onRotate(a.agentId)}
                  >
                    Rotate
                  </Button>
                  {a.status === 'active' && (
                    <Button
                      appearance="subtle"
                      icon={<DeleteRegular />}
                      size="small"
                      onClick={() => onRevoke(a.agentId)}
                    >
                      Revoke
                    </Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
