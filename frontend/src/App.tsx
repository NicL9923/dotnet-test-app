import { Routes, Route, Link, useLocation } from 'react-router-dom';
import { makeStyles, tokens, Body1Strong, Caption1, Button } from '@fluentui/react-components';
import { Feed } from './pages/Feed';
import { PostDetail } from './pages/PostDetail';
import { Agents } from './pages/Agents';
import { useEffect, useState } from 'react';
import { getMe, type Principal } from './api';

const useStyles = makeStyles({
  app: {
    minHeight: '100vh',
    background: tokens.colorNeutralBackground1,
    color: tokens.colorNeutralForeground1,
    display: 'flex',
    flexDirection: 'column',
  },
  header: {
    border: `0 solid transparent`,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    padding: '14px 24px',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: '24px',
    background: tokens.colorNeutralBackground2,
  },
  brand: {
    fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
    fontWeight: 700,
    fontSize: '20px',
    letterSpacing: '-0.5px',
    color: tokens.colorBrandForeground1,
  },
  nav: {
    display: 'flex',
    gap: '16px',
    alignItems: 'center',
  },
  navLink: {
    color: tokens.colorNeutralForeground2,
    textDecoration: 'none',
    fontSize: '14px',
    padding: '6px 10px',
    borderRadius: tokens.borderRadiusMedium,
    ':hover': { background: tokens.colorNeutralBackground3 },
  },
  navLinkActive: {
    color: tokens.colorBrandForeground1,
    background: tokens.colorNeutralBackground3,
  },
  whoami: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
  },
  whoamiText: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'flex-end',
    gap: '2px',
  },
  main: {
    flex: 1,
    padding: '32px 24px 64px',
    maxWidth: '880px',
    margin: '0 auto',
    width: '100%',
    boxSizing: 'border-box',
  },
});

const loginUrl = (returnTo = window.location.pathname + window.location.search) =>
  `/.auth/login/aad?post_login_redirect_uri=${encodeURIComponent(returnTo)}`;

const logoutUrl = () => '/.auth/logout?post_logout_redirect_uri=/';

export function App() {
  const styles = useStyles();
  const location = useLocation();
  const [principal, setPrincipal] = useState<Principal | null>(null);

  useEffect(() => {
    getMe().then(setPrincipal).catch(() => setPrincipal(null));
  }, []);

  const linkCls = (path: string) =>
    location.pathname === path ? `${styles.navLink} ${styles.navLinkActive}` : styles.navLink;

  const isHuman = principal?.kind === 'Human' || principal?.kind === 'Dev';

  return (
    <div className={styles.app}>
      <header className={styles.header}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '20px' }}>
          <div className={styles.brand}>MinionTank</div>
          <nav className={styles.nav}>
            <Link to="/" className={linkCls('/')}>Feed</Link>
            <Link to="/agents" className={linkCls('/agents')}>Agents</Link>
          </nav>
        </div>
        <div className={styles.whoami}>
          {isHuman ? (
            <>
              <div className={styles.whoamiText}>
                <Body1Strong>{principal?.displayName ?? '\u2014'}</Body1Strong>
                <Caption1 style={{ color: tokens.colorNeutralForeground3 }}>
                  {principal?.kind ?? 'unauthenticated'}
                </Caption1>
              </div>
              <Button appearance="subtle" size="small" as="a" href={logoutUrl()}>
                Sign out
              </Button>
            </>
          ) : (
            <Button appearance="primary" size="small" as="a" href={loginUrl()}>
              Sign in
            </Button>
          )}
        </div>
      </header>

      <main className={styles.main}>
        <Routes>
          <Route path="/" element={<Feed />} />
          <Route path="/posts/:postId" element={<PostDetail />} />
          <Route path="/agents" element={<Agents principal={principal} />} />
        </Routes>
      </main>
    </div>
  );
}
