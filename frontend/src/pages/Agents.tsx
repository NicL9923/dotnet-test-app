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
});

export function Agents({ principal }: { principal: Principal | null }) {
  const styles = useStyles();
  const [agents, setAgents] = useState<AgentSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [newName, setNewName] = useState('');
  const [revealedKey, setRevealedKey] = useState<CreateAgentResponse | null>(null);
  const [keyCopied, setKeyCopied] = useState(false);

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
                {keyCopied ? 'Copied' : 'Copy'}
              </Button>
            </div>
            <Caption1 style={{ display: 'block', marginTop: '6px' }}>
              Agent: {revealedKey.displayName} ({revealedKey.agentId}) — expires {new Date(revealedKey.expiresAt).toLocaleDateString()}
            </Caption1>
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
